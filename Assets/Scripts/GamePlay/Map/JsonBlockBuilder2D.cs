using System;
using System.Collections.Generic;
using UnityEngine;
using Dungeon.Player;

namespace Dungeon.Map
{
    #region JSON Data Models (2D용 중복 방지 이름)
    [Serializable]
    public class CellData2D
    {
        public string coord; // "0705" (hex, XXYY)
        public string code;  // "001-001", "000-000", ...
    }

    [Serializable]
    public class PlayerData2D
    {
        public string start; // "0004"
    }

    [Serializable]
    public class DirectionData2D
    {
        public string coord; // "0305"
        public string dir;   // "00"(위), "01"(오), "10"(왼), "11"(아래)
    }

    [Serializable]
    public class RelationData2D
    {
        public string coord_m; // master coord
        public string coord_t; // target coord
    }

    [Serializable]
    public class MapData2D
    {
        public string size;            // 예: "0a07" (hex width, height)
        public CellData2D[] grid;
        public DirectionData2D[] direction;
        public RelationData2D[] relation;
        public PlayerData2D player;
    }
    #endregion

    public class JsonBlockBuilder2D : MonoBehaviour
    {
        [Header("JSON 파일 이름 (Assets/Resources 기준, 확장자 제외)")]
        public string jsonFileName = "map_001";

        [Header("프리팹 Resources 경로 (Resources/Prefabs/{code}.prefab)")]
        public string prefabFolder = "Prefabs/";

        [Header("그리드 설정")]
        public float cellSize = 1f;
        public float baseZ = 0f; // 2D에서는 보통 0 고정

        [Header("생성 제외 코드(프리팹이 없거나 빈칸 취급할 코드)")]
        public List<string> skipCodes = new List<string>();

        [Header("플레이어 / 리스폰")]
        public Transform player;
        public Respawn2D respawn;

        [Header("플레이어 오프셋")]
        public Vector2 playerSpawnOffset = Vector2.zero;

        [Header("Auto Build On Start")]
        public bool autoBuildOnStart = true;

        private readonly Dictionary<Vector2Int, string> codeMap = new();
        private readonly Dictionary<Vector2Int, string> dirMap = new();
        private readonly Dictionary<Vector2Int, List<string>> relationMap = new();
        private readonly Dictionary<string, GameObject> prefabCache = new();

        public MapData2D LoadedMapData { get; private set; }

        private void Start()
        {
            if (autoBuildOnStart)
                BuildFromJson();
        }

        public void BuildFromJsonPath(string resourcesPathWithoutExt)
        {
            jsonFileName = resourcesPathWithoutExt;
            BuildFromJson();
        }

        private float GetZRotationFromDir(string dir)
        {
            return dir switch
            {
                "00" => 0f,    // 위
                "01" => -90f,  // 오른쪽
                "10" => 90f,   // 왼쪽
                "11" => 180f,  // 아래
                _ => 0f
            };
        }

        public void BuildFromJson()
        {
            ClearChildren();
            codeMap.Clear();
            dirMap.Clear();
            relationMap.Clear();
            prefabCache.Clear();

            TextAsset json = Resources.Load<TextAsset>(jsonFileName);
            if (json == null)
            {
                Debug.LogError($"JSON 파일을 찾을 수 없습니다: {jsonFileName}");
                return;
            }

            LoadedMapData = JsonUtility.FromJson<MapData2D>(json.text);
            if (LoadedMapData == null || LoadedMapData.grid == null)
            {
                Debug.LogError("JSON 파싱 실패 또는 grid 데이터 없음");
                return;
            }

            int width = Convert.ToInt32(LoadedMapData.size.Substring(0, 2), 16);
            int height = Convert.ToInt32(LoadedMapData.size.Substring(2, 2), 16);

            CacheGrid(LoadedMapData.grid);
            CacheDirections(LoadedMapData.direction);
            CacheRelations(LoadedMapData.relation);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var c = new Vector2Int(x, y);

                    if (!codeMap.TryGetValue(c, out string code) || string.IsNullOrEmpty(code))
                        continue;

                    code = code.Trim().ToUpperInvariant();
                    if (skipCodes.Contains(code))
                        continue;

                    GameObject prefab = LoadPrefab(code);
                    if (prefab == null)
                    {
                        Debug.LogWarning($"프리팹 없음 → 생성 스킵: {prefabFolder}{code}");
                        continue;
                    }

                    Quaternion rot = Quaternion.identity;
                    if (dirMap.TryGetValue(c, out string dir) && !string.IsNullOrEmpty(dir))
                    {
                        float zRot = GetZRotationFromDir(dir.Trim());
                        rot = Quaternion.Euler(0f, 0f, zRot);
                    }

                    Vector3 pos = new Vector3(x * cellSize, y * cellSize, baseZ);
                    GameObject block = Instantiate(prefab, pos, rot, transform);
                    block.name = $"{code}_{x:X2}{y:X2}";

                    string coordStr = $"{c.x:X2}{c.y:X2}".ToLowerInvariant();

                    // 선택: BlockCoord가 있으면 넣기
                    var bc = block.GetComponent<BlockCoord>();
                    if (bc == null) bc = block.AddComponent<BlockCoord>();
                    bc.SetCoord(coordStr);

                    // 선택: BlockRegistry가 있으면 등록
                    if (BlockRegistry.Instance != null)
                        BlockRegistry.Instance.Register(coordStr, block);

                    // relation 전달 (나중용)
                    if (relationMap.TryGetValue(c, out var targets) && targets != null)
                    {
                        block.SendMessage(
                            "SetRelationTargets",
                            new List<string>(targets),
                            SendMessageOptions.DontRequireReceiver
                        );
                    }
                }
            }

            PositionPlayerAndSetRespawn(LoadedMapData);

            Debug.Log($"2D Build 완료: {jsonFileName}");
        }

        private void CacheGrid(CellData2D[] grid)
        {
            foreach (var cell in grid)
            {
                if (cell == null) continue;
                if (!TryParseCoord(cell.coord, out int x, out int y)) continue;
                codeMap[new Vector2Int(x, y)] = cell.code;
            }
        }

        private void CacheDirections(DirectionData2D[] directions)
        {
            if (directions == null) return;

            foreach (var d in directions)
            {
                if (d == null) continue;
                if (!TryParseCoord(d.coord, out int x, out int y)) continue;
                if (string.IsNullOrEmpty(d.dir)) continue;

                dirMap[new Vector2Int(x, y)] = d.dir;
            }
        }

        private void CacheRelations(RelationData2D[] relations)
        {
            if (relations == null) return;

            foreach (var r in relations)
            {
                if (r == null) continue;

                if (!TryParseCoord(r.coord_m, out int mx, out int my)) continue;
                string t = NormalizeCoord(r.coord_t);
                if (string.IsNullOrEmpty(t)) continue;

                var m = new Vector2Int(mx, my);
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

        private void PositionPlayerAndSetRespawn(MapData2D mapData)
        {
            if (mapData?.player == null || string.IsNullOrEmpty(mapData.player.start))
                return;

            if (!TryParseCoord(mapData.player.start, out int x, out int y))
                return;

            Vector2 spawn2D = new Vector2(x * cellSize, y * cellSize) + playerSpawnOffset;
            Vector3 spawnPos = new Vector3(spawn2D.x, spawn2D.y, 0f);

            if (player != null)
                player.position = spawnPos;

            if (respawn == null && player != null)
                respawn = player.GetComponent<Respawn2D>();

            if (respawn != null)
                respawn.SetRespawnPoint(spawn2D);
            else
                Debug.LogWarning("Respawn2D 컴포넌트를 찾지 못했습니다.");
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;

                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        private static bool TryParseCoord(string coord, out int x, out int y)
        {
            x = y = 0;
            coord = NormalizeCoord(coord);
            if (string.IsNullOrEmpty(coord) || coord.Length < 4) return false;

            try
            {
                x = Convert.ToInt32(coord.Substring(0, 2), 16);
                y = Convert.ToInt32(coord.Substring(2, 2), 16);
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
}