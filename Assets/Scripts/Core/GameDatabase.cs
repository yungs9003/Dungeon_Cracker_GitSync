using System;
using System.Collections.Generic;
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
    public class GameDatabase : MonoBehaviour
    {
        public const string CategoryItem = "item";
        public const string CategoryEquip = "equip";
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

        [Header("Icon Overrides")]
        [Tooltip("fullKey에는 item:000 또는 equip:000 형식으로 넣는 것을 추천합니다. fullKey가 비어 있으면 category + id로 key를 만듭니다.")]
        [SerializeField] private DatabaseIconEntry[] iconOverrides;

        [Header("Fallback Equipment Icons")]
        [SerializeField] private Sprite headFallbackIcon;
        [SerializeField] private Sprite bodyFallbackIcon;
        [SerializeField] private Sprite legsFallbackIcon;
        [SerializeField] private Sprite unknownFallbackIcon;

        private readonly Dictionary<string, ItemDefinition> itemMap = new();
        private readonly Dictionary<string, EquipmentDefinition> equipMap = new();

        private readonly Dictionary<string, DbRecordKind> recordKinds = new();

        private readonly Dictionary<string, List<ItemDefinition>> itemsByType = new();
        private readonly Dictionary<string, List<EquipmentDefinition>> equipsBySlot = new();
        private readonly Dictionary<string, Sprite> iconMap = new();

        private DbRoot loadedData;

        public IReadOnlyList<ItemDefinition> Items => loadedData != null && loadedData.items != null ? loadedData.items : Array.Empty<ItemDefinition>();
        public IReadOnlyList<EquipmentDefinition> Equipment => loadedData != null && loadedData.equips != null ? loadedData.equips : Array.Empty<EquipmentDefinition>();

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
                loadedData = JsonUtility.FromJson<DbRoot>(jsonAsset.text) ?? new DbRoot();
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
            recordKinds.Clear();
            itemsByType.Clear();
            equipsBySlot.Clear();
            iconMap.Clear();

            LoadIconOverrides();
            RebuildItemMaps();
            RebuildEquipmentMaps();
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
                return LoadResourceIcon(itemIconResourceRoot, id);

            if (category == CategoryEquip)
            {
                Sprite loaded = LoadResourceIcon(equipmentIconResourceRoot, id);
                if (loaded != null)
                    return loaded;

                if (TryGetEquipmentByKey(key, out var equip))
                    return GetEquipmentFallbackIcon(equip.slot);
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

        private Sprite LoadResourceIcon(string root, string id)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(id))
                return null;

            return Resources.Load<Sprite>($"{root.TrimEnd('/')}/{NormalizeId(id)}");
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
        Equipment = 2
    }

    [Serializable]
    public class DbRoot
    {
        public ItemDefinition[] items;
        public EquipmentDefinition[] equips;
    }

    [Serializable]
    public class ItemDefinition
    {
        public string id;
        public int rank;
        public string name;
        public string type;
        public string desc;

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
        [Tooltip("권장 형식: item:000 또는 equip:000. 이 값이 있으면 category/id보다 우선합니다.")]
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
