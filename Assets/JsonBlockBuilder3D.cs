using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CellData
{
    public string coord; // "0000"
    public string code;  // "100-000", "000-001", "001-001"
}
[System.Serializable]
public class PlayerData
{
    public string start;
}
[System.Serializable]
public class DirectionData
{
    public string coord; // "0305"
    public string dir;   // "00"(위), "01"(오), "10"(왼), "11"(아래)
}

[System.Serializable]
public class MapData
{
    public string size;   // "0202" → 2x2
    public CellData[] grid;
    public DirectionData[] direction;
    public PlayerData player;
}


public class JsonBlockBuilder3D : MonoBehaviour
{
    [Header("JSON 파일 이름 (Assets/Resources 기준, 확장자 제외)")]
    public string jsonFileName = "map_1130";

    [Header("블록 프리팹 (큐브)")]
    public GameObject blockPrefab;

    [Header("그리드 설정")]
    public float cellSize = 1f;    // x,z 방향 간격
    public float unitHeight = 1f;  // 높이 1일 때 y 스케일 값

    [Header("플레이어 설정")]
    public Transform player;       // 씬에 있는 Player 오브젝트를 드래그해서 넣기
    public float playerYOffset = 0.5f; // 블록 위에서 살짝 띄우는 값

    // 각 좌표별 블록 높이 저장 (게임 로직용)
    private Dictionary<Vector2Int, int> heightMap = new Dictionary<Vector2Int, int>();

    // 좌표 중복 방지
    private HashSet<Vector2Int> usedPositions = new HashSet<Vector2Int>();

    void Start()
    {
        BuildBlocks();
    }
    // ✅ dir(00/01/10/11) → 회전(Quaternion)
    Quaternion GetRotationFromDir(string dir)
    {
        // dir: 00(위), 01(오), 10(왼), 11(아래)
        // 벡터 규칙: 위 (0,0,1), 오 (1,0,0), 왼 (-1,0,0), 아래 (0,0,-1)
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

    void BuildBlocks()
    {
        // 이전에 만들어진 자식 오브젝트 정리 (재생성 대비)
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        heightMap.Clear();
        usedPositions.Clear();

        // 1) JSON 로드
        TextAsset json = Resources.Load<TextAsset>(jsonFileName);
        if (json == null)
        {
            Debug.LogError($"JSON 파일을 찾을 수 없습니다: {jsonFileName}");
            return;
        }

        MapData mapData = JsonUtility.FromJson<MapData>(json.text);
        if (mapData == null || mapData.grid == null)
        {
            Debug.LogError("JSON 파싱 실패 또는 grid 데이터 없음");
            return;
        }

        Debug.Log($"가져온 파일: {jsonFileName} ");

        int width = Convert.ToInt32(mapData.size.Substring(0, 2), 16);
        int height = Convert.ToInt32(mapData.size.Substring(2, 2), 16);
        Debug.Log($"3D Map size: {width} x {height}");
        Dictionary<string, string> dirMap = new Dictionary<string, string>();
        if (mapData.direction != null)
        {
            Debug.Log("something");
            foreach (var d in mapData.direction)
            {
                if (d == null) continue;
                if (string.IsNullOrEmpty(d.coord) || string.IsNullOrEmpty(d.dir)) continue;
                dirMap[d.coord.Trim().ToUpperInvariant()] = d.dir.Trim();
            }
        }

        // 2) 각 셀에 대해 큐브 생성
        foreach (var cell in mapData.grid)
        {
            int x = Convert.ToInt32(cell.coord.Substring(0, 2), 16);
            int y = Convert.ToInt32(cell.coord.Substring(2, 2), 16);

            Vector2Int coord2D = new Vector2Int(x, y);
            if (usedPositions.Contains(coord2D))
            {
                Debug.LogWarning($"중복 좌표 무시: ({x}, {y}), code={cell.code}");
                continue;
            }
            usedPositions.Add(coord2D);

            // code → 높이값으로 변환
            int h = GetHeightFromCode(cell.code);

            // 높이 0 이하면 아무것도 안 만든다 (빈 칸)
            if (h <= 0) continue;

            heightMap[coord2D] = h;

            // 3D 월드 좌표 계산
            // x → worldX, y → worldZ 로 사용 (탑뷰 기준)
            float worldX = x * cellSize;
            float worldZ = y * cellSize;

            // 큐브 pivot이 가운데라서, 높이의 절반만큼 y를 올려준다
            float worldY = (h * unitHeight) / 2f;

            Vector3 worldPos = new Vector3(worldX, worldY, worldZ);

            string prefabPath = "Prefabs/" + cell.code;
            blockPrefab = Resources.Load<GameObject>(prefabPath);
            if (blockPrefab == null)
            {
                Debug.LogError($"프리팹 로드 실패: {prefabPath} (cell.coord={cell.coord}, code={cell.code})");
                continue;
            }

            // ✅ direction 정보가 있는 좌표면 해당 방향으로 회전 적용
            Quaternion rot = Quaternion.identity;
            string coordKey = cell.coord.Trim().ToUpperInvariant();
            
            if (dirMap.TryGetValue(coordKey, out string dir))
            {
                dir = dirMap[coordKey];
                rot = GetRotationFromDir(dir);
            }

            GameObject block = Instantiate(blockPrefab, worldPos, rot, transform);

            // 높이에 맞게 스케일 조정 (y 방향만)
            block.transform.localScale = new Vector3(cellSize, h * unitHeight, cellSize);

            block.name = $"Block_{x}_{y}_h{h}";
        }

        Debug.Log("3D 블록 생성 완료");
        PositionPlayer(mapData);
    }

    void PositionPlayer(MapData mapData)
    {
        if (player == null)
        {
            Debug.LogWarning("Player Transform이 설정되지 않았습니다.");
            return;
        }

        if (mapData.player == null || string.IsNullOrEmpty(mapData.player.start))
        {
            Debug.LogWarning("mapData.player.start 가 비어있습니다.");
            return;
        }

        string start = mapData.player.start;

        if (start.Length < 4)
        {
            Debug.LogWarning($"start 값이 올바르지 않습니다: {start}");
            return;
        }

        // "00 00" (hex) → x, y
        int x = Convert.ToInt32(start.Substring(0, 2), 16);
        int y = Convert.ToInt32(start.Substring(2, 2), 16);

        // 높이 정보 가져오기 (없으면 0)
        int h = GetHeightAt(x, y);

        // 월드 좌표 계산 (블록 생성할 때와 동일한 규칙)
        float worldX = x * cellSize;
        float worldZ = y * cellSize;

        // 블록의 윗면 높이 = h * unitHeight
        // 플레이어는 그 위에 살짝 띄워서 배치
        float worldY = h * unitHeight + playerYOffset;

        Vector3 playerPos = new Vector3(worldX, worldY, worldZ);
        player.position = playerPos;

        Debug.Log($"플레이어를 ({x},{y}) (height={h}) → {playerPos} 위치에 배치했습니다.");
    }




    // code 문자열을 실제 "높이 값"으로 바꿔주는 함수
    int GetHeightFromCode(string code)
    {
        switch (code)
        {
            case "000-000":
                return 1;   // 바닥
            case "100-000": 
                return 0;   // 낭떠러지
            case "002-002":
                return 1;   // 함정해제모듈
            case "001-001":
                return 2;   // 벽
            case "101-001":
                return 2;   // 화살
            default:
                Debug.LogWarning($"알 수 없는 code: {code}");
                return 0;
        }
    }


    // 다른 스크립트에서 높이 정보가 필요할 때 사용
    public int GetHeightAt(int x, int y)
    {
        Vector2Int c = new Vector2Int(x, y);
        if (heightMap.TryGetValue(c, out int h))
            return h;
        return 0;
    }
}
