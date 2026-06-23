using System;
using System.Collections.Generic;
using UnityEngine;
using Dungeon.Player;

namespace Dungeon.Map
{
    [Serializable]
    public class HiddenCellData2D
    {
        public string code;       // 숨겨졌을 때 보여줄 프리팹 코드. 예: "000-000"
        public int assetNo;       // 숨겨졌을 때 보여줄 asset 번호
        public string direction;  // 현재 버전에서는 저장만 하고, 부모 회전에는 적용하지 않음
    }

    [Serializable]
    public class CellData2D
    {
        public string coord;      // "0000"
        public string code;       // "000-000"
        public int assetNo;       // 0,1,2...
        public string direction;  // "00","01","10","11"

        // optional. 없으면 일반 타일처럼 그대로 표시됨.
        public HiddenCellData2D hidden;
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

        [Header("Hidden Tile / Forward Zone Vision")]
        [Tooltip("앞서 만든 ForwardZoneVisionMask2D가 있다면 연결하세요. 비워두면 자동 검색합니다.")]
        public ForwardZoneVisionMask2D visionSystem;

        [Tooltip("한 번 forward zone에 들어와 발견된 타일을 계속 실제 모습으로 유지할지 여부입니다.")]
        public bool hiddenTilesStayRevealed = true;

        [Tooltip("hidden 필드가 없는 예전 맵에서도 102-xxx=>000-000, 101-xxx=>001-001 규칙을 자동 적용할지 여부입니다.")]
        public bool useLegacyTrapDisguiseFallback = false;

        [Tooltip("이 prefix에 해당하는 실제 코드에만 hidden overlay를 허용합니다. 일반 바닥/벽/구멍에는 hidden이 있어도 무시합니다.")]
        public List<string> hiddenAllowedActualCodePrefixes = new List<string> { "101-", "102-" };

        [Tooltip("hidden 대상이 아닌 타일에 남아 있는 HiddenTrapVisual2D나 __HiddenVisualOverlay 자식을 자동 제거합니다. 테스트 중 프리팹에 잘못 붙은 overlay를 제거할 때 유용합니다.")]
        public bool removeStaleHiddenVisualFromNonHiddenTiles = true;

        [Tooltip("자동 제거할 hidden overlay 자식 오브젝트 이름입니다.")]
        public string hiddenOverlayChildName = "__HiddenVisualOverlay";

        [Header("Direction Rotation Policy")]
        [Tooltip("켜면 아래 prefix에 해당하는 코드만 direction으로 회전합니다. 벽/바닥 assetNo 자체가 이미 방향별 이미지라면 켜는 것을 권장합니다.")]
        public bool applyDirectionRotationOnlyToPrefixes = false;

        [Tooltip("applyDirectionRotationOnlyToPrefixes가 켜졌을 때 direction 회전을 적용할 코드 prefix입니다. 예: 101-, 102-")]
        public List<string> directionRotationPrefixes = new List<string> { "101-", "102-" };

        [Header("Debug Tile Build")]
        [Tooltip("켜면 타일 생성 과정 로그를 출력합니다. Debug Only Coord가 비어 있으면 모든 타일을 출력합니다.")]
        public bool logTileBuild = false;

        [Tooltip("특정 좌표만 로그로 보고 싶을 때 입력합니다. 예: 031b, 0000. 비워두면 전체 타일을 출력합니다.")]
        public string debugOnlyCoord = "";

        [Tooltip("켜면 hidden overlay 등록/스프라이트 탐색 결과도 함께 출력합니다.")]
        public bool logHiddenBuild = true;

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

            if (visionSystem == null)
                visionSystem = FindFirstObjectByType<ForwardZoneVisionMask2D>();

            if (visionSystem != null)
                visionSystem.ClearRegisteredTraps();

            if (string.IsNullOrWhiteSpace(jsonFileName))
            {
                Debug.LogError(
                    "JSON 파일 이름이 비어 있습니다. " +
                    "JsonBlockBuilder2D의 Json File Name에 Resources 기준 경로를 입력하세요. " +
                    "예: Maps/Dungeon_001/map_002"
                );
                return;
            }

            TextAsset json = Resources.Load<TextAsset>(jsonFileName);
            if (json == null)
            {
                Debug.LogError(
                    $"JSON 파일을 찾을 수 없습니다: {jsonFileName}\n" +
                    $"실제 파일 위치 예시: Assets/Resources/{jsonFileName}.json"
                );
                return;
            }

            LoadedMapData = JsonUtility.FromJson<MapData2D>(json.text);
            if (LoadedMapData == null || LoadedMapData.grid == null)
            {
                Debug.LogError("JSON 파싱 실패 또는 grid 없음");
                return;
            }

            CacheRelations(LoadedMapData.relation);

            if (logTileBuild)
            {
                Debug.Log(
                    $"[JsonBlockBuilder2D] JSON loaded: {jsonFileName}, " +
                    $"gridCount={LoadedMapData.grid.Length}, " +
                    $"debugOnlyCoord={(string.IsNullOrWhiteSpace(debugOnlyCoord) ? "ALL" : NormalizeCoord(debugOnlyCoord))}"
                );
            }

            foreach (var cell in LoadedMapData.grid)
            {
                if (cell == null) continue;
                if (!TryParseCoord(cell.coord, out int x, out int y)) continue;
                if (string.IsNullOrWhiteSpace(cell.code)) continue;

                string code = cell.code.Trim().ToUpperInvariant();
                bool shouldLogThisTile = ShouldLogTile(cell);

                if (shouldLogThisTile)
                {
                    Debug.Log(
                        $"[BuildTile] coord={NormalizeCoord(cell.coord)}, " +
                        $"actual={code}, assetNo={cell.assetNo}, dir={cell.direction}, " +
                        $"hidden={HiddenInfoToDebugString(cell.hidden)}"
                    );
                }

                if (skipCodes.Contains(code))
                {
                    if (shouldLogThisTile)
                        Debug.Log($"[BuildTile Skip] coord={NormalizeCoord(cell.coord)}, code={code} is in skipCodes.");
                    continue;
                }

                GameObject prefab = LoadPrefab(code);
                if (shouldLogThisTile)
                {
                    Debug.Log(
                        $"[BuildTile Prefab] coord={NormalizeCoord(cell.coord)}, " +
                        $"requestedCode={code}, loadedPrefab={(prefab != null ? prefab.name : "NULL")}, " +
                        $"path={prefabFolder}{code}"
                    );
                }

                if (prefab == null)
                {
                    Debug.LogWarning($"프리팹 없음: {prefabFolder}{code}");
                    continue;
                }

                float zRot = GetAppliedZRotation(code, cell.direction);
                Vector3 pos = new Vector3(x * cellSize, y * cellSize, baseZ);
                Quaternion rot = Quaternion.Euler(0f, 0f, zRot);

                GameObject block = Instantiate(prefab, pos, rot, transform);
                block.name = $"{code}_{x:X2}{y:X2}";

                // 실제 타일 assetNo 적용
                var variant = block.GetComponent<AssetVariantSprite2D>();
                if (variant != null)
                    variant.SetAssetNo(cell.assetNo);

                if (shouldLogThisTile)
                {
                    Debug.Log(
                        $"[BuildTile Result] coord={NormalizeCoord(cell.coord)}, " +
                        $"objectName={block.name}, " +
                        $"variant={(variant != null ? "YES" : "NO")}, " +
                        $"sprite={GetFirstSpriteName(block)}, " +
                        $"rotationZ={block.transform.eulerAngles.z}, " +
                        $"pos=({block.transform.position.x:0.###}, {block.transform.position.y:0.###}, {block.transform.position.z:0.###})"
                    );

                    LogAllSpriteRenderers(block, NormalizeCoord(cell.coord));
                }

                // hidden 정보가 있으면 처음에는 숨김용 스프라이트로 보이게 등록
                SetupHiddenVisualIfNeeded(block, code, cell, shouldLogThisTile);

                // coord 기록용
                string coordStr = $"{x:X2}{y:X2}".ToLowerInvariant();

                var bc = block.GetComponent<BlockCoord>();
                if (bc == null) bc = block.AddComponent<BlockCoord>();
                bc.SetCoord(coordStr);

                if (BlockRegistry.Instance != null)
                    BlockRegistry.Instance.Register(coordStr, block);

                // relation 전달
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

            if (visionSystem != null)
                visionSystem.RebuildMask(LoadedMapData, cellSize, transform);

            Debug.Log($"2D Build 완료: {jsonFileName}");
        }

        private void SetupHiddenVisualIfNeeded(GameObject block, string actualCode, CellData2D cell, bool shouldLogThisTile)
        {
            if (block == null || cell == null)
                return;

            // 핵심 안전장치:
            // hidden 정보가 JSON에 잘못 들어 있거나, 이전 테스트 중 프리팹에 HiddenTrapVisual2D가 남아 있어도
            // 실제 코드가 101-/102- 같은 허용 대상이 아니면 절대 hidden overlay를 만들지 않습니다.
            if (!IsHiddenAllowedActualCode(actualCode))
            {
                if (removeStaleHiddenVisualFromNonHiddenTiles)
                    CleanupStaleHiddenVisual(block);

                if (shouldLogThisTile && logHiddenBuild && HasUsableHiddenInfo(cell.hidden))
                {
                    Debug.Log(
                        $"[HiddenTile Ignored] coord={NormalizeCoord(cell.coord)}, object={block.name}, " +
                        $"actualCode={actualCode}, hidden={HiddenInfoToDebugString(cell.hidden)}. " +
                        "이 actualCode는 hiddenAllowedActualCodePrefixes 대상이 아니어서 hidden 처리를 무시합니다."
                    );
                }

                return;
            }

            HiddenCellData2D hidden = cell.hidden;

            // Unity JsonUtility 환경에서는 JSON에 hidden 필드가 아예 없어도
            // nested class 필드가 null이 아닌 "빈 객체"처럼 들어오는 경우가 있습니다.
            // 따라서 hidden == null 여부가 아니라 hidden.code가 실제로 있는지를 기준으로 처리합니다.
            // hidden 필드가 없거나 hidden.code가 비어 있으면 일반 타일로 보고 조용히 건너뜁니다.
            if (!HasUsableHiddenInfo(hidden))
            {
                if (useLegacyTrapDisguiseFallback)
                    hidden = CreateLegacyHiddenInfo(actualCode, cell.assetNo);

                if (!HasUsableHiddenInfo(hidden))
                {
                    if (shouldLogThisTile && logHiddenBuild)
                    {
                        Debug.Log(
                            $"[HiddenTile Skip] coord={NormalizeCoord(cell.coord)}, object={block.name}, " +
                            "usable hidden 정보가 없어 일반 타일로 처리합니다."
                        );
                    }
                    return;
                }
            }

            Sprite hiddenSprite = GetSpriteFromPrefab(hidden.code, hidden.assetNo);
            if (hiddenSprite == null)
            {
                Debug.LogWarning(
                    $"{block.name}: hidden sprite를 찾지 못했습니다. " +
                    $"coord={NormalizeCoord(cell.coord)}, hidden.code={hidden.code}, assetNo={hidden.assetNo}"
                );
                return;
            }

            if (shouldLogThisTile && logHiddenBuild)
            {
                Debug.Log(
                    $"[HiddenTile Sprite] coord={NormalizeCoord(cell.coord)}, object={block.name}, " +
                    $"hiddenCode={hidden.code}, hiddenAssetNo={hidden.assetNo}, " +
                    $"hiddenDir={(string.IsNullOrWhiteSpace(hidden.direction) ? cell.direction : hidden.direction)}, " +
                    $"sprite={hiddenSprite.name}"
                );
            }

            var hiddenVisual = block.GetComponent<HiddenTrapVisual2D>();
            if (hiddenVisual == null)
                hiddenVisual = block.AddComponent<HiddenTrapVisual2D>();

            float actualZRot = GetAppliedZRotation(actualCode, cell.direction);
            string hiddenDirection = string.IsNullOrWhiteSpace(hidden.direction) ? cell.direction : hidden.direction;
            float hiddenZRot = GetAppliedZRotation(hidden.code, hiddenDirection);

            // 실제 오브젝트의 방향/콜라이더/함정 스크립트는 actualZRot 그대로 유지하고,
            // 숨김 상태에서는 별도 자식 SpriteRenderer만 hiddenZRot 방향으로 얹어서 보여준다.
            hiddenVisual.Configure(hiddenSprite, actualZRot, hiddenZRot, hiddenTilesStayRevealed);

            if (visionSystem != null)
                visionSystem.RegisterTrap(hiddenVisual);

            if (shouldLogThisTile && logHiddenBuild)
            {
                Debug.Log(
                    $"[HiddenTile Registered] coord={NormalizeCoord(cell.coord)}, object={block.name}, " +
                    $"actualZRot={actualZRot}, hiddenZRot={hiddenZRot}, " +
                    $"stayRevealed={hiddenTilesStayRevealed}, visionSystem={(visionSystem != null ? "YES" : "NO")}"
                );
            }
        }

        private bool ShouldLogTile(CellData2D cell)
        {
            if (!logTileBuild || cell == null)
                return false;

            if (string.IsNullOrWhiteSpace(debugOnlyCoord))
                return true;

            return string.Equals(
                NormalizeCoord(cell.coord),
                NormalizeCoord(debugOnlyCoord),
                StringComparison.OrdinalIgnoreCase
            );
        }

        private static string HiddenInfoToDebugString(HiddenCellData2D hidden)
        {
            if (!HasUsableHiddenInfo(hidden))
                return "none";

            return $"code={hidden.code}, assetNo={hidden.assetNo}, dir={hidden.direction}";
        }

        private static string GetFirstSpriteName(GameObject obj)
        {
            if (obj == null)
                return "NULL";

            var sr = obj.GetComponentInChildren<SpriteRenderer>(true);
            if (sr == null)
                return "NO_SPRITE_RENDERER";

            return sr.sprite != null ? sr.sprite.name : "NULL_SPRITE";
        }

        private void LogAllSpriteRenderers(GameObject obj, string coord)
        {
            if (obj == null)
                return;

            var renderers = obj.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                Debug.Log($"[SpriteRenderers] coord={coord}, object={obj.name}, count=0");
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                var sr = renderers[i];
                if (sr == null) continue;

                Debug.Log(
                    $"[SpriteRenderer {i}] coord={coord}, object={obj.name}, " +
                    $"rendererPath={GetTransformPath(sr.transform, obj.transform)}, " +
                    $"enabled={sr.enabled}, sprite={(sr.sprite != null ? sr.sprite.name : "NULL")}, " +
                    $"sortingLayer={sr.sortingLayerName}, sortingOrder={sr.sortingOrder}, " +
                    $"localRotZ={sr.transform.localEulerAngles.z}, worldRotZ={sr.transform.eulerAngles.z}"
                );
            }
        }

        private static string GetTransformPath(Transform target, Transform root)
        {
            if (target == null)
                return "NULL";

            if (root == null || target == root)
                return target.name;

            var stack = new Stack<string>();
            Transform t = target;
            while (t != null)
            {
                stack.Push(t.name);
                if (t == root)
                    break;
                t = t.parent;
            }

            return string.Join("/", stack);
        }

        private static bool HasUsableHiddenInfo(HiddenCellData2D hidden)
        {
            return hidden != null && !string.IsNullOrWhiteSpace(hidden.code);
        }

        private bool IsHiddenAllowedActualCode(string actualCode)
        {
            if (string.IsNullOrWhiteSpace(actualCode))
                return false;

            actualCode = actualCode.Trim().ToUpperInvariant();

            if (hiddenAllowedActualCodePrefixes == null || hiddenAllowedActualCodePrefixes.Count == 0)
                return false;

            foreach (string rawPrefix in hiddenAllowedActualCodePrefixes)
            {
                if (string.IsNullOrWhiteSpace(rawPrefix))
                    continue;

                string prefix = rawPrefix.Trim().ToUpperInvariant();
                if (actualCode.StartsWith(prefix))
                    return true;
            }

            return false;
        }

        private void CleanupStaleHiddenVisual(GameObject block)
        {
            if (block == null)
                return;

            var hiddenVisuals = block.GetComponentsInChildren<HiddenTrapVisual2D>(true);
            foreach (var hv in hiddenVisuals)
            {
                if (hv == null) continue;

                if (Application.isPlaying)
                    Destroy(hv);
                else
                    DestroyImmediate(hv);
            }

            if (!string.IsNullOrWhiteSpace(hiddenOverlayChildName))
            {
                Transform[] children = block.GetComponentsInChildren<Transform>(true);
                foreach (var t in children)
                {
                    if (t == null || t == block.transform)
                        continue;

                    if (t.name != hiddenOverlayChildName)
                        continue;

                    if (Application.isPlaying)
                        Destroy(t.gameObject);
                    else
                        DestroyImmediate(t.gameObject);
                }
            }
        }

        private HiddenCellData2D CreateLegacyHiddenInfo(string actualCode, int actualAssetNo)
        {
            if (string.IsNullOrWhiteSpace(actualCode))
                return null;

            actualCode = actualCode.Trim().ToUpperInvariant();

            if (actualCode.StartsWith("102-"))
            {
                return new HiddenCellData2D
                {
                    code = "000-000",
                    assetNo = actualAssetNo
                };
            }

            if (actualCode.StartsWith("101-"))
            {
                return new HiddenCellData2D
                {
                    code = "001-001",
                    assetNo = actualAssetNo
                };
            }

            return null;
        }

        private Sprite GetSpriteFromPrefab(string code, int assetNo)
        {
            GameObject prefab = LoadPrefab(code);
            if (prefab == null)
                return null;

            var variant = prefab.GetComponent<AssetVariantSprite2D>();
            if (variant != null)
            {
                Sprite sprite = variant.GetSpriteForAssetNo(assetNo, true);
                if (sprite != null)
                    return sprite;
            }

            var sr = prefab.GetComponentInChildren<SpriteRenderer>(true);
            return sr != null ? sr.sprite : null;
        }

        private float GetAppliedZRotation(string code, string dir)
        {
            return ShouldApplyDirectionRotation(code) ? GetZRotationFromDir(dir) : 0f;
        }

        private bool ShouldApplyDirectionRotation(string code)
        {
            if (!applyDirectionRotationOnlyToPrefixes)
                return true;

            if (string.IsNullOrWhiteSpace(code))
                return false;

            code = code.Trim().ToUpperInvariant();

            if (directionRotationPrefixes == null || directionRotationPrefixes.Count == 0)
                return false;

            foreach (string rawPrefix in directionRotationPrefixes)
            {
                if (string.IsNullOrWhiteSpace(rawPrefix))
                    continue;

                string prefix = rawPrefix.Trim().ToUpperInvariant();
                if (code.StartsWith(prefix))
                    return true;
            }

            return false;
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
