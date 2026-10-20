using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Dungeon.Player;

namespace Dungeon.Map
{
    /// <summary>
    /// JSON grid 배열의 한 칸 데이터입니다.
    /// code / assetNo / direction은 실제 생성될 타일 정보를 의미합니다.
    /// 구버전 JSON에 hidden 필드가 남아 있어도 현재 로더는 읽거나 사용하지 않습니다.
    /// </summary>
    [Serializable]
    public class CellData2D
    {
        public string coord;      // 4자리 hex 좌표. 예: "0000", "0a03"
        public string code;       // 실제 생성할 프리팹 코드. 예: "000-000", "101-001"
        public int assetNo;       // 실제 타일의 asset 번호
        public string direction;  // 실제 타일의 방향값. "00", "01", "10", "11"
    }

    [Serializable]
    public class PlayerData2D
    {
        public string start;      // 플레이어 시작 좌표. 예: "0004"
    }

    [Serializable]
    public class RelationData2D
    {
        public string coord_m;    // relation을 받는 메인 타일 좌표
        public string coord_t;    // relation 대상 타일 좌표
    }

    /// <summary>
    /// 맵 전체에 적용되는 필드 효과 데이터입니다.
    /// type은 "fire"(기존 "burn" 호환), "ice", 또는 비어 있는 값/"none"을 사용합니다.
    /// damage는 FieldEffectManager가 주기적으로 적용할 1회 피해량입니다.
    /// </summary>
    [Serializable]
    public class FieldEffectData2D
    {
        public string type;
        public int damage;
    }

    [Serializable]
    public class MapData2D
    {
        public string size;       // 맵 크기. 예: "0a07"
        public CellData2D[] grid;

        // JSON 원본 키는 "3F"입니다.
        // C# 식별자는 숫자로 시작할 수 없으므로, JSON 로딩 직전에 "3F" -> "floor3"로 키를 바꿔서 파싱합니다.
        public CellData2D[] floor3;

        public RelationData2D[] relation;
        public PlayerData2D player;
        public FieldEffectData2D field_effect;
    }

    /// <summary>
    /// Resources에 있는 JSON 맵 파일을 읽어서 2D 타일 프리팹을 생성하는 빌더입니다.
    ///
    /// 핵심 역할:
    /// 1. JSON grid의 code / assetNo / direction을 실제 프리팹에 반영합니다.
    /// 2. relation 정보를 각 타일에 전달합니다.
    /// 3. player.start 위치로 플레이어와 Respawn2D 기준점을 이동합니다.
    /// 4. 발견 대상 함정은 자식 AssetVariant Element를 전환하거나 자식 렌더링을 활성화합니다.
    /// </summary>
    public partial class JsonBlockBuilder2D : MonoBehaviour
    {
        [Header("JSON 파일 이름 (Assets/Resources 기준, 확장자 제외)")]
        public string jsonFileName = "map_001";

        [Header("프리팹 Resources 경로")]
        public string prefabFolder = "Prefabs/";

        [Header("그리드 설정")]
        public float cellSize = 1f;
        public float baseZ = 0f;

        [Header("타일 렌더 순서")]
        [Tooltip("기본 grid 타일 SpriteRenderer들의 기준 sortingOrder입니다.")]
        public int gridSortingOrderBase = 0;

        [Tooltip("3F 타일 SpriteRenderer들의 기준 sortingOrder입니다. grid보다 크게 설정하세요.")]
        public int floor3SortingOrderBase = 100;

        [Header("생성 제외 코드")]
        public List<string> skipCodes = new List<string>();

        [Header("플레이어 / 리스폰")]
        public Transform player;
        public Respawn2D respawn;

        [Header("플레이어 오프셋")]
        public Vector2 playerSpawnOffset = Vector2.zero;

        [Header("Auto Build On Start")]
        public bool autoBuildOnStart = true;

        [Header("Field Effect")]
        [Tooltip("맵의 field_effect를 적용할 FieldEffectManager입니다. 비워두면 자동 검색합니다.")]
        public FieldEffectManager fieldEffectManager;

        [Header("Trap Reveal / Forward Zone Vision")]
        [Tooltip("ForwardZoneVisionMask2D가 있다면 연결하세요. 비워두면 자동 검색합니다.")]
        public ForwardZoneVisionMask2D visionSystem;

        [FormerlySerializedAs("hiddenTilesStayRevealed")]
        [Tooltip("한 번 시야에 들어와 발견된 함정을 계속 발견 상태로 유지할지 여부입니다.")]
        public bool trapTilesStayRevealed = true;

        [Tooltip("Element 전환 또는 자식 렌더러 표시 방식으로 발견 처리할 함정 코드 prefix입니다.")]
        public List<string> trapRevealCodePrefixes = new List<string> { "101-", "102-", "103-" };

        [Tooltip("발견 전환에서 제외할 함정 코드입니다.")]
        public List<string> trapRevealExcludedCodes = new List<string>();

        [Header("Direction Rotation Policy")]
        [Tooltip("켜면 아래 prefix에 해당하는 코드만 direction으로 회전합니다. 벽/바닥 assetNo 자체가 이미 방향별 이미지라면 켜는 것을 권장합니다.")]
        public bool applyDirectionRotationOnlyToPrefixes = false;

        [Tooltip("applyDirectionRotationOnlyToPrefixes가 켜졌을 때 direction 회전을 적용할 코드 prefix입니다. 예: 101-, 102-")]
        public List<string> directionRotationPrefixes = new List<string> { "101-", "102-" };

        private readonly Dictionary<string, GameObject> prefabCache = new();
        private readonly Dictionary<Vector2Int, List<string>> relationMap = new();

        public MapData2D LoadedMapData { get; private set; }
        public FieldEffectManager.FieldEffectType CurrentSpriteTheme { get; private set; }

        private void Start()
        {
            if (autoBuildOnStart)
                BuildFromJson();
        }

        /// <summary>
        /// 외부 매니저가 맵 경로를 지정해서 빌드할 때 사용합니다.
        /// resourcesPathWithoutExt는 Assets/Resources 기준 경로이며 .json 확장자는 제외합니다.
        /// </summary>
        public void BuildFromJsonPath(string resourcesPathWithoutExt)
        {
            jsonFileName = resourcesPathWithoutExt;
            BuildFromJson();
        }

        /// <summary>
        /// 현재 jsonFileName을 기준으로 맵을 새로 생성합니다.
        /// 이전에 이 빌더 아래 생성된 타일은 먼저 제거됩니다.
        /// </summary>
        public void BuildFromJson()
        {
            CurrentSpriteTheme = FieldEffectManager.FieldEffectType.None;
            ResetTileRetileState();
            ClearChildren();
            prefabCache.Clear();
            relationMap.Clear();

            PrepareVisionSystem();

            TextAsset json = LoadMapJson();
            if (json == null)
                return;

            // JSON에는 "3F"라는 키가 있지만 C# 필드명은 숫자로 시작할 수 없습니다.
            // JsonUtility가 읽을 수 있도록 루트 키 이름을 floor3로 바꾼 뒤 역직렬화합니다.
            string normalizedJson = NormalizeSpecialJsonKeys(json.text);

            LoadedMapData = JsonUtility.FromJson<MapData2D>(normalizedJson);
            if (LoadedMapData == null || LoadedMapData.grid == null)
            {
                Debug.LogError("JSON 파싱 실패 또는 grid 없음");
                return;
            }

            CacheRelations(LoadedMapData.relation);
            CurrentSpriteTheme = FieldEffectManager.ParseType(LoadedMapData.field_effect?.type);

            // 렌더 순서: grid < 3F
            BuildGridTiles(LoadedMapData.grid);
            Build3FTiles(LoadedMapData.floor3);

            PositionPlayerAndSetRespawn(LoadedMapData);
            ApplyLoadedFieldEffect(LoadedMapData.field_effect);

            // 마스크는 실제 생성된 맵 데이터 기준으로 다시 만듭니다.
            if (visionSystem != null)
                visionSystem.RebuildMask(LoadedMapData, cellSize, transform);
        }

        /// <summary>
        /// JSON 루트의 field_effect를 FieldEffectManager에 전달합니다.
        /// field_effect가 없거나 type이 none이면 이전 필드 효과를 해제합니다.
        /// </summary>
        private void ApplyLoadedFieldEffect(FieldEffectData2D fieldEffect)
        {
            if (fieldEffectManager == null)
                fieldEffectManager = FindFirstObjectByType<FieldEffectManager>();

            if (fieldEffectManager == null)
            {
                if (fieldEffect != null &&
                    !string.IsNullOrWhiteSpace(fieldEffect.type) &&
                    !string.Equals(fieldEffect.type, "none", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning(
                        "[JsonBlockBuilder2D] field_effect가 있지만 FieldEffectManager를 찾지 못했습니다."
                    );
                }

                return;
            }

            fieldEffectManager.ApplyMapEffect(fieldEffect, player);
        }

        /// <summary>
        /// ForwardZoneVisionMask2D를 준비하고, 이전 맵에서 등록된 발견 대상 함정 목록을 초기화합니다.
        /// </summary>
        private void PrepareVisionSystem()
        {
            if (visionSystem == null)
                visionSystem = FindFirstObjectByType<ForwardZoneVisionMask2D>();

            if (visionSystem != null)
                visionSystem.ClearRegisteredTraps();
        }

        /// <summary>
        /// Resources에서 JSON TextAsset을 불러옵니다.
        /// </summary>
        private TextAsset LoadMapJson()
        {
            if (string.IsNullOrWhiteSpace(jsonFileName))
            {
                Debug.LogError(
                    "JSON 파일 이름이 비어 있습니다. " +
                    "JsonBlockBuilder2D의 Json File Name에 Resources 기준 경로를 입력하세요. " +
                    "예: Maps/Dungeon_001/map_002"
                );
                return null;
            }

            TextAsset json = Resources.Load<TextAsset>(jsonFileName);
            if (json == null)
            {
                Debug.LogError(
                    $"JSON 파일을 찾을 수 없습니다: {jsonFileName}\n" +
                    $"실제 파일 위치 예시: Assets/Resources/{jsonFileName}.json"
                );
            }

            return json;
        }

        /// <summary>
        /// JSON grid 배열을 순회하면서 실제 타일 프리팹을 생성합니다.
        /// </summary>
        private void BuildGridTiles(CellData2D[] cells)
        {
            foreach (var cell in cells)
            {
                if (!TryValidateCell(cell, out int x, out int y, out string code))
                    continue;

                if (skipCodes.Contains(code))
                    continue;

                GameObject prefab = LoadPrefab(code);
                if (prefab == null)
                {
                    Debug.LogWarning($"프리팹 없음: {prefabFolder}{code}");
                    continue;
                }

                GameObject block = CreateTileInstance(prefab, code, cell, x, y);
                ApplyBlockCoordAndRegistry(block, x, y);

                bool isRevealTrap = ShouldConfigureTrapReveal(code);
                int initialAssetNo = isRevealTrap
                    ? (visionSystem != null ? 0 : 1)
                    : cell.assetNo;

                ApplyAssetVariant(block, initialAssetNo);

                // 기본 grid는 가장 아래 렌더 밴드에 둡니다.
                ApplySortingBand(block, gridSortingOrderBase);

                SetupTrapRevealIfNeeded(block, code);
                SendRelationsIfNeeded(block, x, y);
                RegisterTileForRetile(block, cell, false);
            }
        }

        /// <summary>
        /// JSON의 3F 배열을 순회하면서 grid와 같은 좌표 위에 추가 타일을 생성합니다.
        ///
        /// 3F는 실제 GameObject/프리팹으로 생성되므로 해당 프리팹의 트랩 스크립트나 콜라이더도 그대로 동작합니다.
        /// 단, 같은 좌표의 기본 grid 타일을 BlockRegistry에서 덮어쓰지 않도록 Registry에는 등록하지 않습니다.
        /// 렌더 순서는 grid보다 높은 밴드에 둡니다.
        /// </summary>
        private void Build3FTiles(CellData2D[] cells)
        {
            if (cells == null || cells.Length == 0)
                return;

            foreach (var cell in cells)
            {
                if (!TryValidateCell(cell, out int x, out int y, out string code))
                    continue;

                if (skipCodes.Contains(code))
                    continue;

                GameObject prefab = LoadPrefab(code);
                if (prefab == null)
                {
                    Debug.LogWarning($"3F 프리팹 없음: {prefabFolder}{code}");
                    continue;
                }

                GameObject block = CreateTileInstance(prefab, code, cell, x, y);
                block.name = $"3F_{code}_{x:X2}{y:X2}";

                // 좌표 정보는 필요할 수 있으므로 BlockCoord는 붙이되,
                // 같은 coord의 기본 grid 타일을 덮어쓰지 않도록 BlockRegistry에는 등록하지 않습니다.
                ApplyBlockCoordAndRegistry(block, x, y, registerToRegistry: false);

                ApplyAssetVariant(block, cell.assetNo);
                ApplySortingBand(block, floor3SortingOrderBase);
                SetupTrapRevealIfNeeded(block, code, visibilityOnly: true);
                RegisterTileForRetile(block, cell, true);

                // 3F relation 처리는 현재 적용하지 않습니다.
            }
        }

        /// <summary>
        /// grid cell이 실제로 생성 가능한 데이터인지 확인하고, 좌표와 코드 값을 정리합니다.
        /// </summary>
        private bool TryValidateCell(CellData2D cell, out int x, out int y, out string code)
        {
            x = y = 0;
            code = null;

            if (cell == null)
                return false;

            if (!TryParseCoord(cell.coord, out x, out y))
                return false;

            if (string.IsNullOrWhiteSpace(cell.code))
                return false;

            code = cell.code.Trim().ToUpperInvariant();
            return true;
        }

        /// <summary>
        /// 타일 프리팹을 실제 월드 좌표에 생성하고, direction 정책에 따라 회전을 적용합니다.
        /// </summary>
        private GameObject CreateTileInstance(GameObject prefab, string code, CellData2D cell, int x, int y)
        {
            float zRot = GetAppliedZRotation(code, cell.direction);
            Vector3 pos = new Vector3(x * cellSize, y * cellSize, baseZ);
            Quaternion rot = Quaternion.Euler(0f, 0f, zRot);

            GameObject block = Instantiate(prefab, pos, rot, transform);
            block.name = $"{code}_{x:X2}{y:X2}";
            ApplySpriteTheme(block);

            return block;
        }

        /// <summary>생성/교체한 타일의 모든 렌더러와 이후 Element 전환에 현재 맵 테마를 적용합니다.</summary>
        public void ApplySpriteTheme(GameObject block)
        {
            if (block == null) return;
            foreach (var variant in block.GetComponentsInChildren<AssetVariantSprite2D>(true))
                variant.SetSpriteTheme(CurrentSpriteTheme);
            foreach (var renderer in block.GetComponentsInChildren<SpriteRenderer>(true))
                renderer.sprite = MapSpriteThemeCatalog.ResolveDefault(renderer.sprite, CurrentSpriteTheme);
        }

        /// <summary>
        /// BlockCoord와 BlockRegistry에 좌표 정보를 등록합니다.
        /// 다른 타일/함정 스크립트가 좌표 기반으로 이 오브젝트를 찾을 수 있게 하기 위한 단계입니다.
        /// </summary>
        private void ApplyBlockCoordAndRegistry(
            GameObject block,
            int x,
            int y,
            bool registerToRegistry = true)
        {
            string coordStr = $"{x:X2}{y:X2}".ToLowerInvariant();

            var bc = block.GetComponent<BlockCoord>();
            if (bc == null)
                bc = block.AddComponent<BlockCoord>();

            bc.SetCoord(coordStr);

            if (registerToRegistry && BlockRegistry.Instance != null)
                BlockRegistry.Instance.Register(coordStr, block);
        }

        /// <summary>
        /// 프리팹 루트 또는 자식에 AssetVariantSprite2D가 있으면 JSON의 assetNo를 적용합니다.
        /// </summary>
        private static void ApplyAssetVariant(GameObject block, int assetNo)
        {
            AssetVariantSprite2D variant = FindAssetVariant(block);
            if (variant != null)
                variant.SetAssetNo(assetNo);
        }

        private static AssetVariantSprite2D FindAssetVariant(GameObject block)
        {
            if (block == null)
                return null;

            AssetVariantSprite2D rootVariant = block.GetComponent<AssetVariantSprite2D>();
            return rootVariant != null
                ? rootVariant
                : block.GetComponentInChildren<AssetVariantSprite2D>(true);
        }

        /// <summary>
        /// 기본 grid 함정은 자식 AssetVariantSprite2D Element 0/1로 구성하고,
        /// 3F 함정은 자식 SpriteRenderer의 visibility-only 방식으로 구성합니다.
        /// 맵 JSON의 hidden 필드는 조회하지 않습니다.
        /// </summary>
        private void SetupTrapRevealIfNeeded(
            GameObject block,
            string actualCode,
            bool visibilityOnly = false)
        {
            if (block == null ||
                visionSystem == null ||
                !ShouldConfigureTrapReveal(actualCode))
            {
                return;
            }

            HiddenTrapVisual2D revealVisual = block.GetComponent<HiddenTrapVisual2D>();
            if (revealVisual == null)
                revealVisual = block.AddComponent<HiddenTrapVisual2D>();

            bool configured;

            if (visibilityOnly)
            {
                SpriteRenderer[] childRenderers =
                    block.GetComponentsInChildren<SpriteRenderer>(true);

                configured = revealVisual.ConfigureVisibilityOnly(
                    childRenderers,
                    trapTilesStayRevealed
                );
            }
            else
            {
                AssetVariantSprite2D[] variants =
                    block.GetComponentsInChildren<AssetVariantSprite2D>(true);

                if (variants.Length != 1 ||
                    !variants[0].HasVariant(0) ||
                    !variants[0].HasVariant(1))
                {
                    Debug.LogWarning(
                        $"{block.name}: 함정 발견용 AssetVariantSprite2D가 정확히 하나이고 Element 0/1이 모두 지정되어야 합니다. 현재 개수: {variants.Length}"
                    );
                    return;
                }

                configured = revealVisual.ConfigureFromAssetVariants(
                    variants[0],
                    trapTilesStayRevealed
                );
            }

            if (!configured)
                return;

            visionSystem.RegisterTrap(revealVisual);
        }

        /// <summary>
        /// 프리팹 내부 SpriteRenderer들의 기존 상대 sortingOrder 차이는 유지하면서
        /// 전체를 지정된 기준 sortingOrder 밴드로 이동시킵니다.
        ///
        /// 예:
        /// 프리팹 내부 order가 3, 5라면 baseOrder=100 적용 후 100, 102가 됩니다.
        /// </summary>
        private static void ApplySortingBand(GameObject root, int baseOrder)
        {
            if (root == null)
                return;

            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers == null || renderers.Length == 0)
                return;

            int minOrder = int.MaxValue;
            foreach (var sr in renderers)
            {
                if (sr != null && sr.sortingOrder < minOrder)
                    minOrder = sr.sortingOrder;
            }

            if (minOrder == int.MaxValue)
                return;

            foreach (var sr in renderers)
            {
                if (sr == null)
                    continue;

                sr.sortingOrder = baseOrder + (sr.sortingOrder - minOrder);
            }
        }

        /// <summary>
        /// relation이 지정된 타일이면, 해당 타일 스크립트에 relation 대상 목록을 전달합니다.
        /// 받는 쪽이 없을 수도 있으므로 SendMessageOptions.DontRequireReceiver를 사용합니다.
        /// </summary>
        private void SendRelationsIfNeeded(GameObject block, int x, int y)
        {
            var key = new Vector2Int(x, y);
            if (!relationMap.TryGetValue(key, out var targets) || targets == null)
                return;

            block.SendMessage(
                "SetRelationTargets",
                new List<string>(targets),
                SendMessageOptions.DontRequireReceiver
            );
        }

        private bool ShouldConfigureTrapReveal(string actualCode)
        {
            if (string.IsNullOrWhiteSpace(actualCode))
                return false;

            actualCode = actualCode.Trim().ToUpperInvariant();

            // 벽형 이동 함정은 끝부분이 항상 보이며 기존 바닥 함정 Sprite 교체를 사용하지 않습니다.
            if (actualCode == "101-014" || actualCode == "101-015") return false;
            // 테마 함정은 자체 바닥 Sprite/예고 마스크를 사용합니다.
            if (actualCode == "102-016" || actualCode == "102-017" ||
                actualCode == "102-018" || actualCode == "102-019") return false;

            if (trapRevealExcludedCodes != null)
            {
                foreach (string rawExcludedCode in trapRevealExcludedCodes)
                {
                    if (string.Equals(
                        actualCode,
                        rawExcludedCode?.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            if (trapRevealCodePrefixes == null || trapRevealCodePrefixes.Count == 0)
                return false;

            foreach (string rawPrefix in trapRevealCodePrefixes)
            {
                if (string.IsNullOrWhiteSpace(rawPrefix))
                    continue;

                string prefix = rawPrefix.Trim().ToUpperInvariant();
                if (actualCode.StartsWith(prefix))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// direction 회전 정책을 반영한 최종 Z 회전값을 반환합니다.
        /// </summary>
        private float GetAppliedZRotation(string code, string dir)
        {
            return ShouldApplyDirectionRotation(code) ? GetZRotationFromDir(dir) : 0f;
        }

        /// <summary>
        /// 특정 코드에 direction 회전을 적용할지 결정합니다.
        /// applyDirectionRotationOnlyToPrefixes가 꺼져 있으면 모든 타일에 direction을 적용합니다.
        /// </summary>
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

        /// <summary>
        /// JSON의 방향 문자열을 Unity Z 회전값으로 변환합니다.
        /// 현재 프로젝트 기준: 00=위, 01=오른쪽, 10=왼쪽, 11=아래.
        /// </summary>
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

        /// <summary>
        /// relation 배열을 메인 좌표 기준 Dictionary로 캐싱합니다.
        /// 이후 타일 생성 시 빠르게 relation target 목록을 전달하기 위함입니다.
        /// </summary>
        private void CacheRelations(RelationData2D[] relations)
        {
            if (relations == null)
                return;

            foreach (var r in relations)
            {
                if (r == null)
                    continue;

                if (!TryParseCoord(r.coord_m, out int mx, out int my))
                    continue;

                string t = NormalizeCoord(r.coord_t);
                if (string.IsNullOrEmpty(t))
                    continue;

                var key = new Vector2Int(mx, my);
                if (!relationMap.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    relationMap[key] = list;
                }

                list.Add(t);
            }
        }

        /// <summary>
        /// Resources에서 프리팹을 로드합니다.
        /// 같은 code는 여러 번 요청될 수 있으므로 prefabCache로 중복 로드를 줄입니다.
        /// </summary>
        private GameObject LoadPrefab(string code)
        {
            code = code?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code))
                return null;

            if (prefabCache.TryGetValue(code, out var cached) && cached != null)
                return cached;

            string path = prefabFolder + code;
            GameObject prefab = Resources.Load<GameObject>(path);
            prefabCache[code] = prefab;
            return prefab;
        }

        /// <summary>
        /// JSON의 player.start 좌표로 플레이어를 이동시키고 Respawn2D 기준점도 갱신합니다.
        /// </summary>
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

        /// <summary>
        /// 이전 빌드에서 이 빌더의 자식으로 생성된 타일들을 제거합니다.
        /// </summary>
        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;

                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }

        /// <summary>
        /// 4자리 hex 좌표를 x/y 정수 좌표로 변환합니다.
        /// 예: "0a03" → x=10, y=3.
        /// </summary>
        private static bool TryParseCoord(string coord, out int x, out int y)
        {
            x = y = 0;
            coord = NormalizeCoord(coord);
            if (string.IsNullOrEmpty(coord) || coord.Length < 4)
                return false;

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

        /// <summary>
        /// C# 식별자로 직접 선언할 수 없는 JSON 키를 JsonUtility용 내부 키로 바꿉니다.
        /// 현재는 루트의 "3F" 키를 "floor3"로 변환합니다.
        /// </summary>
        private static string NormalizeSpecialJsonKeys(string jsonText)
        {
            if (string.IsNullOrEmpty(jsonText))
                return jsonText;

            return jsonText.Replace("\"3F\"", "\"floor3\"");
        }

        private static string NormalizeCoord(string coord)
        {
            return coord?.Trim().ToLowerInvariant();
        }
    }
}
