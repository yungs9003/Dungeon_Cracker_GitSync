using System;
using System.Collections.Generic;
using UnityEngine;

#region JSON Data Models
[Serializable]
public class CellData
{
    public string coord; // "0705" (hex, XXZZ)
    public string code;  // "001-001", "000-000", ...
}

[Serializable]
public class PlayerData
{
    public string start; // "0004"
}

[Serializable]
public class DirectionData
{
    public string coord; // "0305"
    public string dir;   // "00"(위), "01"(오), "10"(왼), "11"(아래)
}

[Serializable]
public class RelationData
{
    public string coord_m; // master coord
    public string coord_t; // target coord
}

[Serializable]
public class MapData
{
    public string size;            // 예: "0a07" (hex width, depth)
    public CellData[] grid;
    public DirectionData[] direction;
    public RelationData[] relation;
    public PlayerData player;
}
#endregion

/// <summary>
/// 002-002 같은 블록이 relation 타겟(coord_t 리스트)을 받아야 하면,
/// 해당 프리팹에 이 인터페이스를 구현한 컴포넌트를 붙이면 됨.
/// </summary>
public interface IRelationTargetsReceiver
{
    void SetRelationTargets(List<string> coords);
}

public class JsonBlockBuilder3D : MonoBehaviour
{
    [Header("JSON 파일 이름 (Assets/Resources 기준, 확장자 제외)")]
    public string jsonFileName = "map_1130";

    [Header("프리팹 Resources 경로 (Resources/Prefabs/{code}.prefab)")]
    public string prefabFolder = "Prefabs/";

    [Header("그리드 설정")]
    public float cellSize = 1f;   // x,z 간격
    public float baseY = 0f;      // 블록을 둘 기본 바닥 y

    [Header("피벗이 중앙인 프리팹을 바닥에 자동으로 '내려놓기'")]
    public bool autoGroundToBaseY = false;

    [Header("생성 제외 코드(프리팹이 없거나 빈칸 취급할 코드)")]
    public List<string> skipCodes = new List<string>();

    [Header("플레이어 / 리스폰")]
    public Transform player;
    public float playerSpawnY = 1.5f; // 요청: start는 y=1.5 고정
    public Respawn respawn;

    // 내부 캐시
    private readonly Dictionary<Vector2Int, string> codeMap = new Dictionary<Vector2Int, string>();
    private readonly Dictionary<Vector2Int, string> dirMap = new Dictionary<Vector2Int, string>();
    private readonly Dictionary<Vector2Int, List<string>> relationMap = new Dictionary<Vector2Int, List<string>>();
    private readonly Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();

    public MapData LoadedMapData { get; private set; }

    private void Start()
    {
        BuildFromJson();
    }

    // ✅ 회전 방식은 너가 준 그대로 유지 :contentReference[oaicite:3]{index=3}
    Quaternion GetRotationFromDir(string dir)
    {
        Vector3 forward = dir switch
        {
            "00" => new Vector3(0, 0, 1),
            "01" => new Vector3(1, 0, 0),
            "10" => new Vector3(-1, 0, 0),
            "11" => new Vector3(0, 0, -1),
            _ => new Vector3(0, 0, 1)
        };
        return Quaternion.LookRotation(forward, Vector3.up);
    }

    public void BuildFromJson()
    {
        ClearChildren();
        codeMap.Clear();
        dirMap.Clear();
        relationMap.Clear();
        prefabCache.Clear();

        // 1) JSON 로드
        TextAsset json = Resources.Load<TextAsset>(jsonFileName);
        if (json == null)
        {
            Debug.LogError($"JSON 파일을 찾을 수 없습니다: {jsonFileName}");
            return;
        }

        LoadedMapData = JsonUtility.FromJson<MapData>(json.text);
        if (LoadedMapData == null || LoadedMapData.grid == null)
        {
            Debug.LogError("JSON 파싱 실패 또는 grid 데이터 없음");
            return;
        }

        // size: 앞 2자리=width, 뒤 2자리=depth (hex)
        int width = Convert.ToInt32(LoadedMapData.size.Substring(0, 2), 16);
        int depth = Convert.ToInt32(LoadedMapData.size.Substring(2, 2), 16);

        // 2) 데이터 캐싱
        CacheGrid(LoadedMapData.grid);
        CacheDirections(LoadedMapData.direction);
        CacheRelations(LoadedMapData.relation);

        // 3) xz 반복문으로 생성
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                var c = new Vector2Int(x, z);

                if (!codeMap.TryGetValue(c, out string code) || string.IsNullOrEmpty(code))
                    continue;

                code = code.Trim().ToUpperInvariant();
                if (skipCodes.Contains(code))
                    continue;

                // code와 동일 이름 prefab 로드
                GameObject prefab = LoadPrefab(code);
                if (prefab == null)
                {
                    Debug.LogWarning($"프리팹 없음 → 생성 스킵: {prefabFolder}{code}");
                    continue;
                }

                // direction이 있으면 회전 적용
                Quaternion rot = Quaternion.identity;
                if (dirMap.TryGetValue(c, out string dir) && !string.IsNullOrEmpty(dir))
                    rot = GetRotationFromDir(dir.Trim());

                Vector3 pos = new Vector3(x * cellSize, baseY, z * cellSize);
                GameObject block = Instantiate(prefab, pos, rot, transform);
                block.name = $"{code}_{x:X2}{z:X2}";
                // c가 Vector2Int라면:
                string coordStr = $"{c.x:X2}{c.y:X2}".ToLowerInvariant();  // ✅ "030a"
                // ✅ BlockCoord 자동 부착 + 좌표 저장
                var bc = block.GetComponent<BlockCoord>();
                if (bc == null) bc = block.AddComponent<BlockCoord>();
                bc.SetCoord(coordStr);

                // ✅ Registry 등록
                if (BlockRegistry.Instance != null)
                BlockRegistry.Instance.Register(coordStr, block);

                // 피벗이 중앙인 프리팹을 바닥(baseY)에 딱 붙이고 싶을 때 옵션
                if (autoGroundToBaseY)
                    GroundObjectToY(block, baseY);

                // 4) code == "002-002" 이면 relation 전달
                if (code == "002-002" && relationMap.TryGetValue(c, out var targets) && targets != null)
                {
                    Debug.Log($"[Builder] Found 002-002 at {coordStr}");
                    Debug.Log($"[Builder] relationMap has key? {relationMap.ContainsKey(c)}");
                    var receiver = block.GetComponent<IRelationTargetsReceiver>();
                    if (receiver != null)
                        receiver.SetRelationTargets(new List<string>(targets));
                    else
                        block.SendMessage("SetRelationTargets", new List<string>(targets), SendMessageOptions.DontRequireReceiver);
                }
            }
        }

        // 5) player.start 위치로 플레이어 배치 + Respawn에 전달 (y=1.5 고정) :contentReference[oaicite:4]{index=4}
        PositionPlayerAndSetRespawn(LoadedMapData);

        Debug.Log($"Build 완료: {jsonFileName}");
    }

    private void CacheGrid(CellData[] grid)
    {
        foreach (var cell in grid)
        {
            if (cell == null) continue;
            if (!TryParseCoord(cell.coord, out int x, out int z)) continue;
            codeMap[new Vector2Int(x, z)] = cell.code;
        }
    }

    private void CacheDirections(DirectionData[] directions)
    {
        if (directions == null) return;

        foreach (var d in directions)
        {
            if (d == null) continue;
            if (!TryParseCoord(d.coord, out int x, out int z)) continue;
            if (string.IsNullOrEmpty(d.dir)) continue;

            dirMap[new Vector2Int(x, z)] = d.dir;
        }
    }

    private void CacheRelations(RelationData[] relations)
    {
        if (relations == null) return;

        foreach (var r in relations)
        {
            if (r == null) continue;

            if (!TryParseCoord(r.coord_m, out int mx, out int mz)) continue;
            string t = NormalizeCoord(r.coord_t);
            if (string.IsNullOrEmpty(t)) continue;

            var m = new Vector2Int(mx, mz);
            if (!relationMap.TryGetValue(m, out var list))
            {
                list = new List<string>();
                relationMap[m] = list;
            }
            list.Add(t);
        }
    }

    private GameObject LoadPrefab(string code)
    {
        code = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code)) return null;

        if (prefabCache.TryGetValue(code, out var cached) && cached != null)
            return cached;

        string path = prefabFolder + code;
        GameObject prefab = Resources.Load<GameObject>(path);
        prefabCache[code] = prefab;
        return prefab;
    }

    private void PositionPlayerAndSetRespawn(MapData mapData)
    {
        if (mapData?.player == null || string.IsNullOrEmpty(mapData.player.start))
            return;

        if (!TryParseCoord(mapData.player.start, out int x, out int z))
            return;

        Vector3 spawnPos = new Vector3(x * cellSize, playerSpawnY, z * cellSize);

        if (player != null)
            player.position = spawnPos;

        if (respawn == null && player != null)
            respawn = player.GetComponent<Respawn>();

        if (respawn != null)
            respawn.SetRespawnPoint(spawnPos);
        else
            Debug.LogWarning("Respawn 컴포넌트를 찾지 못했습니다. (Inspector에 연결하거나 Player에 Respawn.cs를 붙여줘)");
    }

    private void GroundObjectToY(GameObject go, float targetY)
    {
        // Renderer/Collider bounds 중 더 “믿을만한 것”이 있는 쪽으로 바닥 정렬
        Bounds? b = null;

        var cols = go.GetComponentsInChildren<Collider>();
        if (cols != null && cols.Length > 0)
        {
            b = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++) b = Encapsulate(b.Value, cols[i].bounds);
        }
        else
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends != null && rends.Length > 0)
            {
                b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b = Encapsulate(b.Value, rends[i].bounds);
            }
        }

        if (b.HasValue)
        {
            float delta = targetY - b.Value.min.y;
            go.transform.position += new Vector3(0f, delta, 0f);
        }
    }

    private Bounds Encapsulate(Bounds a, Bounds b)
    {
        a.Encapsulate(b);
        return a;
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }

    private static bool TryParseCoord(string coord, out int x, out int z)
    {
        x = z = 0;
        coord = NormalizeCoord(coord);
        if (string.IsNullOrEmpty(coord) || coord.Length < 4) return false;

        try
        {
            x = Convert.ToInt32(coord.Substring(0, 2), 16);
            z = Convert.ToInt32(coord.Substring(2, 2), 16);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeCoord(string coord)
    {
        return coord?.Trim().ToLowerInvariant();
    }
}
