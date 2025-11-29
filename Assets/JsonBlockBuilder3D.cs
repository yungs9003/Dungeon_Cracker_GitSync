using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CellData
{
    public string coord;
    public string code;
}

[System.Serializable]
public class MapData
{
    public string size;
    public CellData[] grid;
}

public class JsonBlockBuilder3D : MonoBehaviour
{
    [Header("JSON 파일 이름 (Assets/Resources 기준, 확장자 제외)")]
    public string jsonFileName = "map_0202";

    [Header("블록 프리팹 (큐브)")]
    public GameObject blockPrefab;

    [Header("그리드 설정")]
    public float cellSize = 1f;    // x,z 방향 간격
    public float unitHeight = 1f;  // 높이 1일 때 y 스케일 값

    // 각 좌표별 블록 높이 저장 (게임 로직용)
    private Dictionary<Vector2Int, int> heightMap = new Dictionary<Vector2Int, int>();

    // 좌표 중복 방지
    private HashSet<Vector2Int> usedPositions = new HashSet<Vector2Int>();

    void Start()
    {
        BuildBlocks();
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

        int width  = Convert.ToInt32(mapData.size.Substring(0, 2), 16);
        int height = Convert.ToInt32(mapData.size.Substring(2, 2), 16);
        Debug.Log($"3D Map size: {width} x {height}");

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

            GameObject block = Instantiate(blockPrefab, worldPos, Quaternion.identity, transform);

            // 높이에 맞게 스케일 조정 (y 방향만)
            block.transform.localScale = new Vector3(cellSize, h * unitHeight, cellSize);

            block.name = $"Block_{x}_{y}_h{h}";
        }

        Debug.Log("3D 블록 생성 완료");
    }

    // code 문자열을 실제 "높이 값"으로 바꿔주는 함수
    int GetHeightFromCode(string code)
    {
        switch (code)
        {
            case "100-000":
                return 0;   // 빈 칸
            case "000-001":
                return 1;   // 높이 1
            case "001-001":
                return 2;   // 높이 2
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
