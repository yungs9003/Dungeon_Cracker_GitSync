using System;
using System.Collections.Generic;
using UnityEngine;
using Dungeon.Player;

namespace Dungeon.Map
{
    [Serializable]
    public class CellData2D
    {
        public string coord;      // "0000"
        public string code;       // "000-000"
        public int assetNo;       // 0,1,2...
        public string direction;  // "00","01","10","11"
    }

    [Serializable]
    public class PlayerData2D
    {
        public string start; // "0004"
    }

    [Serializable]
    public class RelationData2D
    {
        public string coord_m;
        public string coord_t;
    }

    [Serializable]
    public class MapData2D
    {
        public string size; // 예: "0a07"
        public CellData2D[] grid;
        public RelationData2D[] relation;
        public PlayerData2D player;
    }

    public class JsonBlockBuilder2D : MonoBehaviour
    {
        [Header("JSON 파일 이름 (Assets/Resources 기준, 확장자 제외)")]
        public string jsonFileName = "map_001";

        [Header("프리팹 Resources 경로")]
        public string prefabFolder = "Prefabs/";

        [Header("그리드 설정")]
        public float cellSize = 1f;
        public float baseZ = 0f;

        [Header("생성 제외 코드")]
        public List<string> skipCodes = new List<string>();

        [Header("플레이어 / 리스폰")]
        public Transform player;
        public Respawn2D respawn;

        [Header("플레이어 오프셋")]
        public Vector2 playerSpawnOffset = Vector2.zero;

        [Header("Auto Build On Start")]
        public bool autoBuildOnStart = true;

        private readonly Dictionary<string, GameObject> prefabCache = new();
        private readonly Dictionary<Vector2Int, List<string>> relationMap = new();

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

        public void BuildFromJson()
        {
            ClearChildren();
            prefabCache.Clear();
            relationMap.Clear();

            TextAsset json = Resources.Load<TextAsset>(jsonFileName);
            if (json == null)
            {
                Debug.LogError($"JSON 파일을 찾을 수 없습니다: {jsonFileName}");
                return;
            }

            LoadedMapData = JsonUtility.FromJson<MapData2D>(json.text);
            if (LoadedMapData == null || LoadedMapData.grid == null)
            {
                Debug.LogError("JSON 파싱 실패 또는 grid 없음");
                return;
            }

            CacheRelations(LoadedMapData.relation);

            foreach (var cell in LoadedMapData.grid)
            {
                if (cell == null) continue;
                if (!TryParseCoord(cell.coord, out int x, out int y)) continue;
                if (string.IsNullOrWhiteSpace(cell.code)) continue;

                string code = cell.code.Trim().ToUpperInvariant();
                if (skipCodes.Contains(code))
                    continue;

                GameObject prefab = LoadPrefab(code);
                if (prefab == null)
                {
                    Debug.LogWarning($"프리팹 없음: {prefabFolder}{code}");
                    continue;
                }

                float zRot = GetZRotationFromDir(cell.direction);
                Vector3 pos = new Vector3(x * cellSize, y * cellSize, baseZ);
                Quaternion rot = Quaternion.Euler(0f, 0f, zRot);

                GameObject block = Instantiate(prefab, pos, rot, transform);
                block.name = $"{code}_{x:X2}{y:X2}";

                // assetNo 적용
                var variant = block.GetComponent<AssetVariantSprite2D>();
                if (variant != null)
                    variant.SetAssetNo(cell.assetNo);

                // coord 기록용 (선택)
                string coordStr = $"{x:X2}{y:X2}".ToLowerInvariant();

                var bc = block.GetComponent<BlockCoord>();
                if (bc == null) bc = block.AddComponent<BlockCoord>();
                bc.SetCoord(coordStr);

                if (BlockRegistry.Instance != null)
                    BlockRegistry.Instance.Register(coordStr, block);

                // relation 전달 (필요 시)
                var key = new Vector2Int(x, y);
                if (relationMap.TryGetValue(key, out var targets) && targets != null)
                {
                    block.SendMessage(
                        "SetRelationTargets",
                        new List<string>(targets),
                        SendMessageOptions.DontRequireReceiver
                    );
                }
            }

            PositionPlayerAndSetRespawn(LoadedMapData);

            Debug.Log($"2D Build 완료: {jsonFileName}");
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

        private void CacheRelations(RelationData2D[] relations)
        {
            if (relations == null) return;

            foreach (var r in relations)
            {
                if (r == null) continue;
                if (!TryParseCoord(r.coord_m, out int mx, out int my)) continue;
                string t = NormalizeCoord(r.coord_t);
                if (string.IsNullOrEmpty(t)) continue;

                var key = new Vector2Int(mx, my);
                if (!relationMap.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    relationMap[key] = list;
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
                Debug.LogWarning("Respawn2D를 찾지 못했습니다.");
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