using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class JsonTilemapBuilder : MonoBehaviour
{
    [Header("JSON 파일 이름 (Assets/Resources 기준, 확장자 제외)")]
    public string jsonFileName = "map_0202";

    [Header("타일맵")]
    public Tilemap tilemap;

    [Header("블록 타일 (높이별)")]
    public TileBase height1Tile; // "000-001" → 높이 1
    public TileBase height2Tile; // "001-001" → 높이 2

    // 각 좌표의 블록 높이 저장용 (없으면 0으로 취급)
    private Dictionary<Vector2Int, int> heightMap = new Dictionary<Vector2Int, int>();

    // 중복 좌표 방지
    private HashSet<Vector2Int> usedPositions = new HashSet<Vector2Int>();

    void Start()
    {
        BuildFromJson();
    }

    void BuildFromJson()
    {
        tilemap.ClearAllTiles();
        heightMap.Clear();
        usedPositions.Clear();

        // 1) JSON 읽기
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
        Debug.Log($"Map size: {width} x {height}");

        // 2) 셀들 순회하면서 타일 & 높이 세팅
        foreach (var cell in mapData.grid)
        {
            int x = Convert.ToInt32(cell.coord.Substring(0, 2), 16);
            int y = Convert.ToInt32(cell.coord.Substring(2, 2), 16);

            Vector2Int coord = new Vector2Int(x, y);

            // 좌표 중복 방지
            if (usedPositions.Contains(coord))
            {
                Debug.LogWarning($"중복 좌표 무시: ({x}, {y}), code={cell.code}");
                continue;
            }
            usedPositions.Add(coord);

            // 유니티 타일맵 좌표 (원하면 y에 - 붙이지 않고 그대로 써도 됨)
            Vector3Int tilePos = new Vector3Int(x, -y, 0);

            // 코드에 따라 처리
            switch (cell.code)
            {
                case "100-000":
                    // 빈 칸: 타일 없음, 높이 정보도 안 넣음(0으로 취급)
                    // tilemap.SetTile(tilePos, null); // 굳이 명시적으로 비우고 싶으면
                    break;

                case "000-001":
                    // 높이 1 블록
                    if (height1Tile != null)
                        tilemap.SetTile(tilePos, height1Tile);
                    else
                        Debug.LogWarning($"height1Tile이 비어있음. coord={cell.coord}");

                    heightMap[coord] = 1;
                    break;

                case "001-001":
                    // 높이 2 블록
                    if (height2Tile != null)
                        tilemap.SetTile(tilePos, height2Tile);
                    else
                        Debug.LogWarning($"height2Tile이 비어있음. coord={cell.coord}");

                    heightMap[coord] = 2;
                    break;

                default:
                    Debug.LogWarning($"알 수 없는 code: {cell.code}");
                    break;
            }
        }

        Debug.Log("타일맵 + 높이 정보 생성 완료");
    }

    /// <summary>
    /// (x, y) 좌표의 블록 높이를 가져오는 함수 (없으면 0 반환)
    /// 월드 좌표 → 그리드 좌표 변환해서 쓰는 버전도 따로 만들 수 있음.
    /// </summary>
    public int GetHeightAt(int x, int y)
    {
        Vector2Int coord = new Vector2Int(x, y);
        if (heightMap.TryGetValue(coord, out int h))
            return h;
        return 0; // 블록 없음
    }
}
