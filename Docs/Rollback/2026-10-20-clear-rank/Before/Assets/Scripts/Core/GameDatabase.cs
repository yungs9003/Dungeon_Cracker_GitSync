using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Dungeon.Core
{
    /// <summary>
    /// db.json 전체를 관리하는 공용 데이터베이스입니다.
    ///
    /// 핵심 설계:
    /// - items의 id "000"과 equips의 id "000"은 서로 다른 데이터입니다.
    /// - 그래서 내부 key는 반드시 category를 포함한 "item:000", "equip:000" 형식으로 관리합니다.
    /// - savefile에는 기존 구조를 유지하기 위해 raw id("000")만 저장하고,
    ///   UI/DB 조회 단계에서만 category key로 변환합니다.
    ///
    /// 권장 배치:
    /// - db.json -> Assets/Resources/Data/db.json
    /// - catalogResourcePath -> "Data/db"
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameDatabase : MonoBehaviour
    {
        public const string CategoryItem = "item";
        public const string CategoryEquip = "equip";
        public const string CategoryModule = "module";
        public const string CategoryTrap = "trap";
        public const string NoneKey = "none";

        public static GameDatabase Instance { get; private set; }

        [Header("JSON")]
        [Tooltip("Resources 안의 db.json 경로입니다. 확장자 .json은 쓰지 않습니다. 예: Data/db")]
        [SerializeField] private string catalogResourcePath = "Data/db";

        [Tooltip("테스트용입니다. 여기에 db.json TextAsset을 직접 넣으면 Resources 경로보다 우선합니다.")]
        [SerializeField] private TextAsset databaseJsonOverride;

        [Header("Icon Resources")]
        [Tooltip("소비 아이템 아이콘 Resources 폴더입니다. 예: UI/ItemIcons/000")]
        [SerializeField] private string itemIconResourceRoot = "UI/ItemIcons";

        [Tooltip("장비 아이콘 Resources 폴더입니다. 예: UI/EquipIcons/000")]
        [SerializeField] private string equipmentIconResourceRoot = "UI/EquipIcons";

        [Tooltip("Module icon folder under Resources. Example: UI/ModuleIcons/000")]
        [SerializeField] private string moduleIconResourceRoot = "UI/ModuleIcons";

        [Header("Icon Overrides")]
        [Tooltip("fullKey에는 item:000, equip:000, module:000 형식으로 넣는 것을 추천합니다. fullKey가 비어 있으면 category + id로 key를 만듭니다.")]
        [SerializeField] private DatabaseIconEntry[] iconOverrides;

        [Header("Fallback Equipment Icons")]
        [SerializeField] private Sprite headFallbackIcon;
        [SerializeField] private Sprite bodyFallbackIcon;
        [SerializeField] private Sprite legsFallbackIcon;
        [SerializeField] private Sprite unknownFallbackIcon;

        private readonly Dictionary<string, ItemDefinition> itemMap = new();
        private readonly Dictionary<string, EquipmentDefinition> equipMap = new();
        private readonly Dictionary<string, ModuleDefinition> moduleMap = new();
        private readonly Dictionary<string, TrapDefinition> trapMap = new();
        private readonly Dictionary<string, DungeonDefinition> dungeonMap = new();

        private readonly Dictionary<string, DbRecordKind> recordKinds = new();

        private readonly Dictionary<string, List<ItemDefinition>> itemsByType = new();
        private readonly Dictionary<string, List<EquipmentDefinition>> equipsBySlot = new();
        private readonly Dictionary<string, Sprite> iconMap = new();

        private DbRoot loadedData;

        public IReadOnlyList<ItemDefinition> Items => loadedData != null && loadedData.items != null ? loadedData.items : Array.Empty<ItemDefinition>();
        public IReadOnlyList<EquipmentDefinition> Equipment => loadedData != null && loadedData.equips != null ? loadedData.equips : Array.Empty<EquipmentDefinition>();
        public IReadOnlyList<ModuleDefinition> Modules => loadedData != null && loadedData.modules != null ? loadedData.modules : Array.Empty<ModuleDefinition>();
        public IReadOnlyList<TrapDefinition> Traps => loadedData != null && loadedData.traps != null ? loadedData.traps : Array.Empty<TrapDefinition>();
        public IReadOnlyList<DungeonDefinition> Dungeons => loadedData?.dungeons ?? Array.Empty<DungeonDefinition>();
        public bool TryGetDungeonBySceneName(string sceneName, out DungeonDefinition definition) =>
            dungeonMap.TryGetValue(DungeonProgressService.SceneKey(sceneName), out definition);

        public static bool TryFindDungeonBySceneName(string sceneName, out DungeonDefinition definition)
        {
            if (Instance != null) return Instance.TryGetDungeonBySceneName(sceneName, out definition);
            // 월드맵 씬을 단독 실행해도 공용 DB의 던전 이름/번호를 사용합니다.
            definition = null;
            var json = Resources.Load<TextAsset>("Data/db");
            if (json == null) return false;
            var root = JsonConvert.DeserializeObject<DbRoot>(json.text);
            foreach (var entry in root?.dungeons ?? Array.Empty<DungeonDefinition>())
                if (entry != null && DungeonProgressService.SceneKey(entry.sceneName) == DungeonProgressService.SceneKey(sceneName))
                { definition = entry; return true; }
            return false;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadDatabase();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || Instance != null)
                return;

            // hot reload 후 초기화되지 않은 조회 사전으로 UI가 갱신되지 않게 합니다.
            Instance = this;
            LoadDatabase();
        }

        public void LoadDatabase()
        {
            TextAsset jsonAsset = databaseJsonOverride;

            if (jsonAsset == null)
                jsonAsset = Resources.Load<TextAsset>(catalogResourcePath);

            if (jsonAsset == null)
            {
                Debug.LogError($"[GameDatabase] db.json을 찾을 수 없습니다. Resources 경로: {catalogResourcePath}.json");
                loadedData = new DbRoot();
                RebuildMaps();
                return;
            }

            try
            {
                loadedData = JsonConvert.DeserializeObject<DbRoot>(jsonAsset.text) ?? new DbRoot();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameDatabase] db.json 파싱 실패: {e}");
                loadedData = new DbRoot();
            }

            RebuildMaps();
        }

        private void RebuildMaps()
        {
            itemMap.Clear();
            equipMap.Clear();
            moduleMap.Clear();
            trapMap.Clear();
            recordKinds.Clear();
            itemsByType.Clear();
            equipsBySlot.Clear();
            iconMap.Clear();

            LoadIconOverrides();
            RebuildItemMaps();
            RebuildEquipmentMaps();
            RebuildModuleMaps();
            RebuildTrapMaps();
            dungeonMap.Clear();
            foreach (var dungeon in Dungeons)
            {
                if (dungeon == null || string.IsNullOrWhiteSpace(dungeon.sceneName)) continue;
                dungeon.sceneName = DungeonProgressService.SceneKey(dungeon.sceneName);
                dungeonMap[dungeon.sceneName] = dungeon;
            }
        }

        private void LoadIconOverrides()
        {
            if (iconOverrides == null)
                return;

            foreach (var entry in iconOverrides)
            {
                if (entry == null || entry.sprite == null)
                    continue;

                string key = entry.GetNormalizedKey();
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                iconMap[key] = entry.sprite;
            }
        }

        private void RebuildItemMaps()
        {
            if (loadedData == null || loadedData.items == null)
                return;

            foreach (var item in loadedData.items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.id))
                    continue;

                item.id = NormalizeId(item.id);
                item.type = NormalizeCategoryLikeValue(item.type);

                string key = MakeKey(CategoryItem, item.id);
                itemMap[key] = item;
                recordKinds[key] = DbRecordKind.Item;

                string type = string.IsNullOrWhiteSpace(item.type) ? "unknown" : item.type;
                if (!itemsByType.TryGetValue(type, out var list))
                {
                    list = new List<ItemDefinition>();
                    itemsByType[type] = list;
                }
                list.Add(item);
            }
        }

        private void RebuildEquipmentMaps()
        {
            if (loadedData == null || loadedData.equips == null)
                return;

            foreach (var equip in loadedData.equips)
            {
                if (equip == null || string.IsNullOrWhiteSpace(equip.id))
                    continue;

                equip.id = NormalizeId(equip.id);
                equip.slot = NormalizeSlot(equip.slot);

                string key = MakeKey(CategoryEquip, equip.id);
                equipMap[key] = equip;
                recordKinds[key] = DbRecordKind.Equipment;

                string slot = string.IsNullOrWhiteSpace(equip.slot) ? "unknown" : equip.slot;
                if (!equipsBySlot.TryGetValue(slot, out var list))
                {
                    list = new List<EquipmentDefinition>();
                    equipsBySlot[slot] = list;
                }
                list.Add(equip);
            }
        }

        private void RebuildModuleMaps()
        {
            if (loadedData == null || loadedData.modules == null)
                return;

            foreach (ModuleDefinition module in loadedData.modules)
            {
                if (module == null || string.IsNullOrWhiteSpace(module.id))
                    continue;

                module.id = NormalizeId(module.id);
                string key = MakeKey(CategoryModule, module.id);
                moduleMap[key] = module;
                recordKinds[key] = DbRecordKind.Module;
            }
        }

        private void RebuildTrapMaps()
        {
            if (loadedData == null || loadedData.traps == null)
                return;

            foreach (var trap in loadedData.traps)
            {
                if (trap == null || string.IsNullOrWhiteSpace(trap.code))
                    continue;

                trap.code = NormalizeTrapCode(trap.code);
                trap.damage_type = NormalizeCategoryLikeValue(trap.damage_type);
                trapMap[trap.code] = trap;
                recordKinds[MakeKey(CategoryTrap, trap.code)] = DbRecordKind.Trap;
            }
        }

        public bool TryGetItem(string rawId, out ItemDefinition definition)
        {
            return TryGetItemByKey(MakeKey(CategoryItem, rawId), out definition);
        }

        public bool TryGetItemByKey(string key, out ItemDefinition definition)
        {
            key = NormalizeKey(key);
            return itemMap.TryGetValue(key, out definition);
        }

        public bool TryGetEquipment(string rawId, out EquipmentDefinition definition)
        {
            return TryGetEquipmentByKey(MakeKey(CategoryEquip, rawId), out definition);
        }

        public bool TryGetEquipmentByKey(string key, out EquipmentDefinition definition)
        {
            key = NormalizeKey(key);
            return equipMap.TryGetValue(key, out definition);
        }

        public bool TryGetModule(string rawId, out ModuleDefinition definition)
        {
            return TryGetModuleByKey(MakeKey(CategoryModule, rawId), out definition);
        }

        public bool TryGetModuleByKey(string key, out ModuleDefinition definition)
        {
            key = NormalizeKey(key);
            return moduleMap.TryGetValue(key, out definition);
        }

        public bool TryGetTrap(string prefabCode, out TrapDefinition definition)
        {
            prefabCode = NormalizeTrapCode(prefabCode);
            return trapMap.TryGetValue(prefabCode, out definition);
        }

        public bool ContainsKey(string key)
        {
            key = NormalizeKey(key);
            return recordKinds.ContainsKey(key);
        }

        public bool TryGetKind(string key, out DbRecordKind kind)
        {
            key = NormalizeKey(key);
            return recordKinds.TryGetValue(key, out kind);
        }

        public IReadOnlyList<ItemDefinition> GetAllItems()
        {
            return Items;
        }

        public IReadOnlyList<EquipmentDefinition> GetAllEquipment()
        {
            return Equipment;
        }

        public IReadOnlyList<ModuleDefinition> GetAllModules()
        {
            return Modules;
        }

        public IReadOnlyList<TrapDefinition> GetAllTraps()
        {
            return Traps;
        }

        public IReadOnlyList<ItemDefinition> GetItemsByType(string type)
        {
            type = NormalizeCategoryLikeValue(type);
            if (itemsByType.TryGetValue(type, out var list))
                return list;
            return Array.Empty<ItemDefinition>();
        }

        public IReadOnlyList<EquipmentDefinition> GetEquipmentBySlot(string slot)
        {
            slot = NormalizeSlot(slot);
            if (equipsBySlot.TryGetValue(slot, out var list))
                return list;
            return Array.Empty<EquipmentDefinition>();
        }

        public string GetItemKey(string rawId)
        {
            return MakeKey(CategoryItem, rawId);
        }

        public string GetEquipmentKey(string rawId)
        {
            return MakeKey(CategoryEquip, rawId);
        }

        public string GetModuleKey(string rawId)
        {
            return MakeKey(CategoryModule, rawId);
        }

        public Sprite GetIconByKey(string key)
        {
            key = NormalizeKey(key);
            if (string.IsNullOrWhiteSpace(key) || key == NoneKey)
                return null;

            if (iconMap.TryGetValue(key, out var overrideSprite) && overrideSprite != null)
                return overrideSprite;

            if (!TryParseKey(key, out string category, out string id))
                return unknownFallbackIcon;

            if (category == CategoryItem)
            {
                Sprite loaded = LoadResourceIcon(itemIconResourceRoot, id);
                return loaded != null ? loaded : LoadResourceIcon("UI/ItemsIcons", id);
            }

            if (category == CategoryEquip)
            {
                Sprite loaded = LoadResourceIcon(equipmentIconResourceRoot, id);
                if (loaded != null)
                    return loaded;

                if (TryGetEquipmentByKey(key, out var equip))
                    return GetEquipmentFallbackIcon(equip.slot);
            }

            if (category == CategoryModule)
            {
                Sprite loaded = LoadResourceIcon(moduleIconResourceRoot, id);
                return loaded != null ? loaded : LoadTemporaryModuleIcon(id);
            }

            return unknownFallbackIcon;
        }

        public Sprite GetItemIcon(string rawId)
        {
            return GetIconByKey(MakeKey(CategoryItem, rawId));
        }

        public Sprite GetEquipmentIcon(string rawId)
        {
            return GetIconByKey(MakeKey(CategoryEquip, rawId));
        }

        public Sprite GetModuleIcon(string rawId)
        {
            return GetIconByKey(MakeKey(CategoryModule, rawId));
        }

        // 보유 목록은 임시/대체 이미지 없이 실제 ModuleIcons가 있는 모듈만 표시합니다.
        public Sprite GetModuleResourceIcon(string rawId)
        {
            return LoadResourceIcon(moduleIconResourceRoot, NormalizeId(rawId));
        }

        private Sprite LoadResourceIcon(string root, string id)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(id))
                return null;

            string resourcePath = $"{root.TrimEnd('/')}/{NormalizeId(id)}";
            Sprite icon = Resources.Load<Sprite>(resourcePath);
            if (icon != null)
                return icon;

            // 다중 스프라이트로 가져온 PNG도 모듈 아이콘으로 사용할 수 있습니다.
            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            return sprites.Length > 0 ? sprites[0] : null;
        }

        private Sprite GetEquipmentFallbackIcon(string slot)
        {
            slot = NormalizeSlot(slot);
            return slot switch
            {
                "head" => headFallbackIcon != null ? headFallbackIcon : unknownFallbackIcon,
                "body" => bodyFallbackIcon != null ? bodyFallbackIcon : unknownFallbackIcon,
                "legs" => legsFallbackIcon != null ? legsFallbackIcon : unknownFallbackIcon,
                _ => unknownFallbackIcon
            };
        }

        public static string MakeKey(string category, string id)
        {
            category = NormalizeCategory(category);
            id = NormalizeId(id);

            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(id) || id == NoneKey)
                return NoneKey;

            return $"{category}:{id}";
        }

        public static bool TryParseKey(string key, out string category, out string id)
        {
            category = string.Empty;
            id = string.Empty;

            key = NormalizeKey(key);
            if (string.IsNullOrWhiteSpace(key) || key == NoneKey)
                return false;

            int colon = key.IndexOf(':');
            if (colon <= 0 || colon >= key.Length - 1)
                return false;

            category = NormalizeCategory(key.Substring(0, colon));
            id = NormalizeId(key.Substring(colon + 1));
            return !string.IsNullOrWhiteSpace(category) && !string.IsNullOrWhiteSpace(id);
        }

        public static string NormalizeKey(string key)
        {
            key = key?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(key) || key == NoneKey)
                return NoneKey;

            if (TryParseKeyWithoutNormalizeRecursion(key, out string category, out string id))
                return MakeKey(category, id);

            return key;
        }

        private static bool TryParseKeyWithoutNormalizeRecursion(string key, out string category, out string id)
        {
            category = string.Empty;
            id = string.Empty;
            int colon = key.IndexOf(':');
            if (colon <= 0 || colon >= key.Length - 1)
                return false;

            category = key.Substring(0, colon);
            id = key.Substring(colon + 1);
            return true;
        }

        public static string NormalizeCategory(string category)
        {
            category = category?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(category))
                return string.Empty;

            return category switch
            {
                "items" => CategoryItem,
                "item" => CategoryItem,
                "inventory_item" => CategoryItem,
                "equips" => CategoryEquip,
                "equip" => CategoryEquip,
                "equipment" => CategoryEquip,
                "equipement" => CategoryEquip,
                "modules" => CategoryModule,
                "module" => CategoryModule,
                "traps" => CategoryTrap,
                "trap" => CategoryTrap,
                _ => category
            };
        }

        public static string NormalizeCategoryLikeValue(string value)
        {
            return value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        public static string NormalizeId(string id)
        {
            id = id?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(id))
                return NoneKey;
            return id;
        }

        private Sprite LoadTemporaryModuleIcon(string id)
        {
            if (string.IsNullOrWhiteSpace(moduleIconResourceRoot))
                return unknownFallbackIcon;

            // 숫자 아이콘(000~013)이 준비되면 위의 정식 경로가 항상 우선합니다.
            // 그 전까지는 현재 보유한 세 이미지를 순환해 UI 기능을 테스트합니다.
            string[] fallbackNames =
            {
                "Module-M.DEF",
                "Module-DEF",
                "Module-DEF (1)"
            };

            int index = 0;
            if (int.TryParse(NormalizeId(id), out int numericId))
                index = Math.Abs(numericId) % fallbackNames.Length;

            return Resources.Load<Sprite>(
                $"{moduleIconResourceRoot.TrimEnd('/')}/{fallbackNames[index]}"
            );
        }

        public static string NormalizeTrapCode(string code)
        {
            return code?.Trim().ToUpperInvariant() ?? string.Empty;
        }

        public static string NormalizeSlot(string slot)
        {
            slot = slot?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(slot))
                return string.Empty;

            return slot switch
            {
                "head" => "head",
                "helmet" => "head",
                "body" => "body",
                "armor" => "body",
                "leg" => "legs",
                "legs" => "legs",
                "shoe" => "legs",
                "shoes" => "legs",
                _ => slot
            };
        }
    }

    public enum DbRecordKind
    {
        Unknown = 0,
        Item = 1,
        Equipment = 2,
        Trap = 3,
        Module = 4
    }

    [Serializable]
    public class DbRoot
    {
        public ItemDefinition[] items;
        public EquipmentDefinition[] equips;
        public ModuleDefinition[] modules;
        public TrapDefinition[] traps;
        public DungeonDefinition[] dungeons;
    }

    [Serializable]
    public sealed class DungeonDefinition
    {
        public string sceneName, name, mapResourcePath;
        public int number;
    }

    [Serializable]
    public class ItemDefinition
    {
        public string id;
        public int rank;
        public string name;
        public string type;
        public string desc;
        // 가격이 없는 기존 항목은 무료 판매로 처리하지 않습니다.
        public int gold = -1;

        public string Key => GameDatabase.MakeKey(GameDatabase.CategoryItem, id);
    }

    [Serializable]
    public class EquipmentDefinition
    {
        public string id;
        public int rank;
        public string name;
        public string slot;
        public string desc;

        // db.json의 desc에서 단순 수치 보정으로 해석 가능한 효과를 구조화해서 넣습니다.
        // 예: "최대체력 +50" -> main_stats.max_HP = 50
        // 예: "물리저항 +10" -> main_stats.physical_resist = 10
        // 예: "이동속도 2배" -> sub_stats_multiplier.speed = 2
        public EquipmentMainStatBonus main_stats;
        public EquipmentSubStatBonus sub_stats;
        public EquipmentMainStatMultiplier main_stats_multiplier;
        public EquipmentSubStatMultiplier sub_stats_multiplier;

        public string Key => GameDatabase.MakeKey(GameDatabase.CategoryEquip, id);
    }

    [Serializable]
    public class ModuleDefinition
    {
        public string id;
        public string name;
        public string desc;

        public string Key => GameDatabase.MakeKey(GameDatabase.CategoryModule, id);
    }

    [Serializable]
    public class TrapDefinition
    {
        public string code;
        public string name;
        public string type;
        public string desc;
        public bool? destructible;
        public string damage_type;
        public int? damage;
        public float? knockback_distance;
    }

    [Serializable]
    public class EquipmentMainStatBonus
    {
        public int max_HP;
        public int physical_resist;
        public int magical_resist;
    }

    [Serializable]
    public class EquipmentSubStatBonus
    {
        public int speed;
        public int poison_resist;
        public int electric_resist;
        public int amber_resist;
        public int freeze_resist;
    }

    [Serializable]
    public class EquipmentMainStatMultiplier
    {
        public float max_HP;
        public float physical_resist;
        public float magical_resist;
    }

    [Serializable]
    public class EquipmentSubStatMultiplier
    {
        public float speed;
        public float poison_resist;
        public float electric_resist;
        public float amber_resist;
        public float freeze_resist;
    }

    [Serializable]
    public class DatabaseIconEntry
    {
        [Tooltip("권장 형식: item:000, equip:000, module:000. 이 값이 있으면 category/id보다 우선합니다.")]
        public string fullKey;

        [Tooltip("fullKey를 쓰지 않을 때 사용합니다. item 또는 equip.")]
        public string category = GameDatabase.CategoryEquip;

        [Tooltip("fullKey를 쓰지 않을 때 사용합니다. 예: 000")]
        public string id;

        public Sprite sprite;

        public string GetNormalizedKey()
        {
            if (!string.IsNullOrWhiteSpace(fullKey))
                return GameDatabase.NormalizeKey(fullKey);

            return GameDatabase.MakeKey(category, id);
        }
    }
}
