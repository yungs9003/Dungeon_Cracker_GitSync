using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dungeon.Core;

namespace Dungeon.UI
{
    /// <summary>
    /// 소비 아이템 페이지 전체를 관리합니다.
    ///
    /// 동작 순서:
    /// 1. 위쪽 QuickSlotUI를 클릭합니다.
    /// 2. 선택된 퀵슬롯 번호를 기억합니다.
    /// 3. 아래 소비 아이템 목록을 클릭합니다.
    /// 4. 선택한 퀵슬롯에 해당 아이템을 등록합니다.
    /// </summary>
    public class ConsumablePageUI : MonoBehaviour
    {
        [Header("Database")]
        [SerializeField] private GameDatabase database;

        [Header("Quick Slots")]
        [SerializeField] private QuickSlotUI[] quickSlots;

        [Header("Inventory List")]
        [SerializeField] private Transform itemGridContent;
        [SerializeField]
        private ConsumableItemSlotUI itemSlotPrefab;

        [SerializeField] private Scrollbar verticalScrollbar;

        [Tooltip("db.json에서 표시할 아이템 type입니다.")]
        [SerializeField] private string consumableType = "consumable";

        [Tooltip("Grid Layout Group의 Constraint Count와 맞추세요.")]
        [SerializeField] private int gridColumnCount = 6;

        [Tooltip("화면에 표시할 수 있는 기본 줄 수입니다.")]
        [SerializeField] private int visibleRowsBeforeScrollbar = 2;

        [Header("Debug")]
        [SerializeField] private bool logActions = true;
        [SerializeField] private bool logMissingIcons = true;

        private readonly List<GameObject> spawnedItemSlots = new();

        private QuickSlotUI selectedQuickSlot;
        private bool quickSlotEventsBound;

        private void Awake()
        {
            AutoBindReferences();
            HideSceneTemplateIfNeeded();
        }

        private void OnEnable()
        {
            AutoBindReferences();
            BindQuickSlotEvents();
            // 최초 진입 시에도 모든 FrameImage 색상을 명시적으로 설정합니다.
            RefreshQuickSlotSelectionVisuals();
            HideSceneTemplateIfNeeded();
            ReloadFromSave();
        }

        private void OnDisable()
        {
            UnbindQuickSlotEvents();
        }

        /// <summary>
        /// 세이브 파일을 기준으로 퀵슬롯과 아이템 목록을 갱신합니다.
        /// </summary>
        public void ReloadFromSave()
        {
            if (database == null)
                database = GameDatabase.Instance;

            if (database == null)
            {
                Debug.LogWarning(
                    "[ConsumablePageUI] GameDatabase가 없습니다."
                );

                ClearItemList();
                return;
            }

            if (StatusSaveFile.Instance == null)
            {
                Debug.LogWarning(
                    "[ConsumablePageUI] StatusSaveFile이 없습니다."
                );

                ClearItemList();
                return;
            }

            RefreshAllQuickSlots();
            PopulateInventoryList();
        }

        /// <summary>
        /// 현재 보유 중인 consumable 아이템을 목록에 생성합니다.
        /// </summary>
        private void PopulateInventoryList()
        {
            ClearItemList();
            HideSceneTemplateIfNeeded();

            if (database == null ||
                itemGridContent == null ||
                itemSlotPrefab == null)
            {
                Debug.LogWarning(
                    "[ConsumablePageUI] 목록 생성 참조가 부족합니다. " +
                    "database/itemGridContent/itemSlotPrefab을 확인하세요."
                );

                RefreshScrollbar(0);
                return;
            }

            StatusSaveFile saveFile = StatusSaveFile.Instance;

            if (saveFile == null ||
                saveFile.Data == null ||
                saveFile.Data.inventory == null)
            {
                RefreshScrollbar(0);
                return;
            }

            Dictionary<string, int> ownedCounts =
                CollectOwnedItemCounts(
                    saveFile.Data.inventory.item
                );

            List<OwnedConsumableEntry> visibleItems = new();

            foreach (KeyValuePair<string, int> pair in ownedCounts)
            {
                string rawId = pair.Key;
                int count = pair.Value;

                if (count <= 0)
                    continue;

                string itemKey = GameDatabase.MakeKey(
                    GameDatabase.CategoryItem,
                    rawId
                );

                if (!database.TryGetItemByKey(
                        itemKey,
                        out ItemDefinition definition))
                {
                    Debug.LogWarning(
                        $"[ConsumablePageUI] db.json에서 " +
                        $"아이템을 찾지 못했습니다: {itemKey}"
                    );

                    continue;
                }

                string definitionType =
                    GameDatabase.NormalizeCategoryLikeValue(
                        definition.type
                    );

                string targetType =
                    GameDatabase.NormalizeCategoryLikeValue(
                        consumableType
                    );

                if (definitionType != targetType)
                    continue;

                visibleItems.Add(
                    new OwnedConsumableEntry(
                        definition,
                        count
                    )
                );
            }

            // rank가 낮은 아이템부터, 같은 rank라면 id순으로 표시합니다.
            visibleItems.Sort((left, right) =>
            {
                int rankCompare =
                    left.definition.rank.CompareTo(
                        right.definition.rank
                    );

                if (rankCompare != 0)
                    return rankCompare;

                return string.CompareOrdinal(
                    left.definition.id,
                    right.definition.id
                );
            });

            int shownCount = 0;

            foreach (OwnedConsumableEntry entry in visibleItems)
            {
                ItemDefinition capturedDefinition =
                    entry.definition;

                int capturedCount = entry.count;

                Sprite icon =
                    database.GetIconByKey(
                        capturedDefinition.Key
                    );

                if (icon == null && logMissingIcons)
                {
                    Debug.LogWarning(
                        $"[ConsumablePageUI] 소비 아이템 아이콘을 " +
                        $"찾지 못했습니다. " +
                        $"key={capturedDefinition.Key}, " +
                        $"name={capturedDefinition.name}"
                    );
                }

                ConsumableItemSlotUI slotView =
                    Instantiate(
                        itemSlotPrefab,
                        itemGridContent
                    );

                slotView.gameObject.name =
                    $"ConsumableSlot_" +
                    $"{capturedDefinition.Key}_" +
                    $"{capturedDefinition.name}";

                slotView.gameObject.SetActive(true);

                bool isEquipped =
                    IsRegisteredInAnyQuickSlot(
                        capturedDefinition.Key
                    );

                slotView.SetupConsumable(
                    capturedDefinition,
                    capturedCount,
                    icon,
                    isEquipped,
                    () => OnInventoryItemClicked(
                        capturedDefinition
                    )
                );

                spawnedItemSlots.Add(slotView.gameObject);
                shownCount++;
            }

            if (logActions)
            {
                Debug.Log(
                    $"[ConsumablePageUI] 소비 아이템 목록 생성 완료. " +
                    $"shownCount={shownCount}"
                );
            }

            RefreshScrollbar(shownCount);
        }
        /// <summary>
        /// 선택된 퀵슬롯은 빨간색,
        /// 나머지 퀵슬롯은 기본색으로 갱신합니다.
        /// 선택된 슬롯이 없으면 모두 기본색이 됩니다.
        /// </summary>
        private void RefreshQuickSlotSelectionVisuals()
        {
            if (quickSlots == null)
                return;
            foreach (QuickSlotUI slot in quickSlots)
                {
                    if (slot == null)
                    continue;
                    slot.SetSelected(
                    slot == selectedQuickSlot
                    );
                }
        }

        private bool IsRegisteredInAnyQuickSlot(
            string itemKey)
        {
            return FindQuickSlotContainingItem(itemKey) != null;
        }

        /// <summary>
        /// 해당 아이템이 이미 등록되어 있는 퀵슬롯을 찾습니다.
        /// 같은 종류의 소비 아이템은 여러 퀵슬롯에 중복 등록할 수 없습니다.
        /// </summary>
        private QuickSlotUI FindQuickSlotContainingItem(
            string itemKey)
        {
            if (quickSlots == null ||
                string.IsNullOrWhiteSpace(itemKey))
            {
                return null;
            }

            string normalizedKey =
                GameDatabase.NormalizeKey(itemKey);

            if (normalizedKey == GameDatabase.NoneKey)
                return null;

            foreach (QuickSlotUI slot in quickSlots)
            {
                if (slot == null)
                    continue;

                string registeredKey =
                    GameDatabase.NormalizeKey(
                        slot.CurrentItemKey
                    );

                if (registeredKey == normalizedKey)
                    return slot;
            }

            return null;
        }

        /// <summary>
        /// 위쪽 퀵슬롯을 좌클릭했을 때 호출됩니다.
        /// </summary>
        private void OnQuickSlotClicked(
            QuickSlotUI clickedSlot)
        {
            selectedQuickSlot = clickedSlot;

            RefreshQuickSlotSelectionVisuals();

            if (logActions && selectedQuickSlot != null)
            {
                Debug.Log(
                    $"[ConsumablePageUI] Quick slot selected: " +
                    $"{selectedQuickSlot.SlotNumber}"
                );
            }
        }

        /// <summary>
        /// 위쪽 퀵슬롯을 우클릭하면 등록된 아이템을 해제합니다.
        /// 현재 선택 상태는 유지해서 다른 아이템을 바로 등록할 수 있습니다.
        /// </summary>
        private void OnQuickSlotRightClicked(
            QuickSlotUI clickedSlot)
        {
            if (clickedSlot == null || !clickedSlot.HasItem)
                return;

            // 페이지를 처음 연 직후에는 selectedQuickSlot이 아직 null일 수 있습니다.
            // 우클릭으로 해제한 슬롯을 곧바로 다시 등록 대상으로 사용할 수 있도록
            // 해제 전에 해당 슬롯을 선택 상태로 만듭니다.
            selectedQuickSlot = clickedSlot;
            RefreshQuickSlotSelectionVisuals();

            if (!clickedSlot.ClearItem())
            {
                Debug.LogWarning(
                    $"[ConsumablePageUI] 퀵슬롯 해제 실패. " +
                    $"slot={clickedSlot.SlotNumber}"
                );

                return;
            }

            RefreshAllQuickSlots();
            RefreshQuickSlotSelectionVisuals();
            PopulateInventoryList();

            if (logActions)
            {
                Debug.Log(
                    $"[ConsumablePageUI] Quick slot cleared. " +
                    $"slot={clickedSlot.SlotNumber}"
                );
            }
        }

        /// <summary>
        /// 아래 목록의 소비 아이템을 눌렀을 때 호출됩니다.
        /// </summary>
        private void OnInventoryItemClicked(
            ItemDefinition definition)
        {
            if (logActions)
            {
                Debug.Log(
                    $"[ConsumablePageUI] Inventory item click received. " +
                    $"key={(definition != null ? definition.Key : "null")}, " +
                    $"selectedSlot={(selectedQuickSlot != null ? selectedQuickSlot.SlotNumber.ToString() : "none")}"
                );
            }

            if (definition == null)
                return;

            if (selectedQuickSlot == null)
            {
                Debug.LogWarning(
                    "[ConsumablePageUI] 먼저 위쪽 퀵슬롯을 선택하세요."
                );

                return;
            }

            string normalizedType =
                GameDatabase.NormalizeCategoryLikeValue(
                    definition.type
                );

            string targetType =
                GameDatabase.NormalizeCategoryLikeValue(
                    consumableType
                );

            if (normalizedType != targetType)
            {
                Debug.LogWarning(
                    $"[ConsumablePageUI] 소비 아이템이 아닙니다: " +
                    $"{definition.name}, type={definition.type}"
                );

                return;
            }

            // 이미 다른 퀵슬롯에 등록된 아이템이라면 중복 등록하지 않습니다.
            // 위치를 바꾸려면 기존 슬롯을 우클릭해 먼저 해제해야 합니다.
            QuickSlotUI occupiedSlot =
                FindQuickSlotContainingItem(
                    definition.Key
                );

            if (occupiedSlot != null)
            {
                Debug.LogWarning(
                    $"[ConsumablePageUI] 이미 퀵슬롯에 등록된 아이템입니다. " +
                    $"item={definition.Key}, " +
                    $"registeredSlot={occupiedSlot.SlotNumber}. " +
                    $"기존 슬롯을 우클릭해 해제한 뒤 다시 등록하세요."
                );

                return;
            }

            bool registered =
                selectedQuickSlot.RegisterItem(
                    definition.Key
                );

            if (!registered)
            {
                Debug.LogWarning(
                    $"[ConsumablePageUI] 퀵슬롯 등록 실패. " +
                    $"slot={selectedQuickSlot.SlotNumber}, " +
                    $"item={definition.Key}"
                );

                return;
            }

            if (logActions)
            {
                Debug.Log(
                    $"[ConsumablePageUI] Quick slot registered. " +
                    $"slot={selectedQuickSlot.SlotNumber}, " +
                    $"key={definition.Key}, " +
                    $"name={definition.name}"
                );
            }

            // 아이템창과 HUD의 슬롯 및 목록의 E 표시를 모두 갱신합니다.
            RefreshAllQuickSlots();
            PopulateInventoryList();
        }

        private void RefreshAllQuickSlots()
        {
            if (quickSlots == null)
                return;

            foreach (QuickSlotUI slot in quickSlots)
            {
                if (slot != null)
                    slot.RefreshFromSave();
            }
        }

        private void BindQuickSlotEvents()
        {
            if (quickSlotEventsBound ||
                quickSlots == null)
            {
                return;
            }

            bool boundAny = false;

            foreach (QuickSlotUI slot in quickSlots)
            {
                if (slot == null)
                    continue;

                slot.Clicked += OnQuickSlotClicked;
                slot.RightClicked += OnQuickSlotRightClicked;
                boundAny = true;
            }

            quickSlotEventsBound = boundAny;
        }

        private void UnbindQuickSlotEvents()
        {
            if (!quickSlotEventsBound ||
                quickSlots == null)
            {
                return;
            }

            foreach (QuickSlotUI slot in quickSlots)
            {
                if (slot != null)
                {
                    slot.Clicked -= OnQuickSlotClicked;
                    slot.RightClicked -= OnQuickSlotRightClicked;
                }
            }

            quickSlotEventsBound = false;
        }

        private void AutoBindReferences()
        {
            if (database == null)
                database = GameDatabase.Instance;

            if (quickSlots == null ||
                quickSlots.Length == 0)
            {
                quickSlots =
                    GetComponentsInChildren<QuickSlotUI>(
                        true
                    );

                Array.Sort(
                    quickSlots,
                    (left, right) =>
                    {
                        if (left == null && right == null)
                            return 0;

                        if (left == null)
                            return 1;

                        if (right == null)
                            return -1;

                        return left.SlotNumber.CompareTo(
                            right.SlotNumber
                        );
                    }
                );
            }

            if (itemGridContent == null)
            {
                Transform target = FindChildRecursive(
                    transform,
                    "ConsumableItemGridContent"
                );

                if (target != null)
                    itemGridContent = target;
            }

            if (verticalScrollbar == null)
            {
                Transform target = FindChildRecursive(
                    transform,
                    "Scrollbar Vertical"
                );

                if (target != null)
                {
                    verticalScrollbar =
                        target.GetComponent<Scrollbar>();
                }
            }

            if (itemSlotPrefab == null)
            {
                Transform target = FindChildRecursive(
                    transform,
                    "ConsumableItemSlot"
                );

                if (target != null)
                {
                    itemSlotPrefab =
                        target.GetComponent<
                            ConsumableItemSlotUI
                        >();
                }
            }
        }

        private void HideSceneTemplateIfNeeded()
        {
            if (itemSlotPrefab == null ||
                itemGridContent == null)
            {
                return;
            }

            if (itemSlotPrefab.transform ==
                    itemGridContent ||
                itemSlotPrefab.transform.IsChildOf(
                    itemGridContent
                ))
            {
                itemSlotPrefab.gameObject.SetActive(false);
            }
        }

        private void ClearItemList()
        {
            for (int i = spawnedItemSlots.Count - 1;
                 i >= 0;
                 i--)
            {
                if (spawnedItemSlots[i] != null)
                    Destroy(spawnedItemSlots[i]);
            }

            spawnedItemSlots.Clear();
        }

        private void RefreshScrollbar(int shownCount)
        {
            if (verticalScrollbar == null)
                return;

            int columns =
                Mathf.Max(1, gridColumnCount);

            int visibleRows =
                Mathf.Max(
                    1,
                    visibleRowsBeforeScrollbar
                );

            bool needScrollbar =
                shownCount > columns * visibleRows;

            verticalScrollbar.gameObject.SetActive(
                needScrollbar
            );
        }

        private static Dictionary<string, int>
            CollectOwnedItemCounts(
                List<Dictionary<string, int>> itemList)
        {
            Dictionary<string, int> result = new();

            if (itemList == null)
                return result;

            foreach (Dictionary<string, int> map
                     in itemList)
            {
                if (map == null)
                    continue;

                foreach (KeyValuePair<string, int> pair
                         in map)
                {
                    string rawId =
                        GameDatabase.NormalizeId(
                            pair.Key
                        );

                    if (rawId == GameDatabase.NoneKey)
                        continue;

                    if (!result.ContainsKey(rawId))
                        result[rawId] = 0;

                    result[rawId] +=
                        Mathf.Max(0, pair.Value);
                }
            }

            return result;
        }

        private static Transform FindChildRecursive(
            Transform root,
            string targetName)
        {
            if (root == null ||
                string.IsNullOrWhiteSpace(targetName))
            {
                return null;
            }

            if (root.name == targetName)
                return root;

            for (int i = 0;
                 i < root.childCount;
                 i++)
            {
                Transform found =
                    FindChildRecursive(
                        root.GetChild(i),
                        targetName
                    );

                if (found != null)
                    return found;
            }

            return null;
        }

        private readonly struct OwnedConsumableEntry
        {
            public readonly ItemDefinition definition;
            public readonly int count;

            public OwnedConsumableEntry(
                ItemDefinition definition,
                int count)
            {
                this.definition = definition;
                this.count = count;
            }
        }
    }
}