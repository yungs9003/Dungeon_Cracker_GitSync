using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dungeon.Core;

namespace Dungeon.UI
{
    /// <summary>
    /// ExpendablesPage의 아이템 목록을 관리합니다.
    ///
    /// GameDatabase의 items 배열에서
    /// type이 "consumable"이 아닌 모든 아이템을 표시합니다.
    ///
    /// 현재 클릭은 이벤트만 발생시키며,
    /// 아이템 사용이나 등록 기능은 수행하지 않습니다.
    /// </summary>
    public class ExpendablesPageUI : MonoBehaviour
    {
        [Header("Database")]
        [SerializeField] private GameDatabase database;

        [Header("Inventory List")]
        [SerializeField] private Transform itemGridContent;
        [SerializeField] private ExpendableSlotUI itemSlotPrefab;
        [SerializeField] private Scrollbar verticalScrollbar;

        [Header("Filter")]
        [Tooltip("이 타입을 목록에서 제외합니다.")]
        [SerializeField] private string excludedType = "consumable";

        [Header("Grid")]
        [Tooltip("Grid Layout Group의 Constraint Count와 맞추세요.")]
        [SerializeField] private int gridColumnCount = 6;

        [Tooltip("두 번째 페이지에서 한 화면에 보이는 줄 수입니다.")]
        [SerializeField] private int visibleRowsBeforeScrollbar = 3;

        [Header("Display")]
        [Tooltip("savefile의 보유 개수를 CountText에 표시합니다.")]
        [SerializeField] private bool showOwnedCount = true;

        [Header("Debug")]
        [SerializeField] private bool logActions;
        [SerializeField] private bool logMissingIcons = true;

        private readonly List<GameObject> spawnedSlots =
            new List<GameObject>();

        /// <summary>
        /// 현재는 실제 기능이 연결되지 않은 클릭 이벤트입니다.
        /// 나중에 다른 스크립트에서 구독해 사용할 수 있습니다.
        /// </summary>
        public event Action<ItemDefinition> ItemClicked;

        private void Awake()
        {
            AutoBindReferences();
            HideSceneTemplate();
        }

        private void OnEnable()
        {
            ReloadItems();
        }

        /// <summary>
        /// DB의 전체 items를 다시 읽어 목록을 생성합니다.
        /// </summary>
        public void ReloadItems()
        {
            AutoBindReferences();
            HideSceneTemplate();
            ClearSpawnedSlots();

            if (database == null)
            {
                Debug.LogWarning(
                    "[ExpendablesPageUI] GameDatabase가 없습니다.",
                    this
                );

                RefreshScrollbar(0);
                return;
            }

            if (itemGridContent == null ||
                itemSlotPrefab == null)
            {
                Debug.LogWarning(
                    "[ExpendablesPageUI] Item Grid Content 또는 " +
                    "Item Slot Prefab이 연결되지 않았습니다.",
                    this
                );

                RefreshScrollbar(0);
                return;
            }

            string normalizedExcludedType =
                GameDatabase.NormalizeCategoryLikeValue(
                    excludedType
                );

            List<ItemDefinition> visibleItems =
                new List<ItemDefinition>();

            IReadOnlyList<ItemDefinition> allItems =
                database.GetAllItems();

            foreach (ItemDefinition definition in allItems)
            {
                if (definition == null)
                    continue;

                string normalizedType =
                    GameDatabase.NormalizeCategoryLikeValue(
                        definition.type
                    );

                // consumable만 제외하고 나머지는 모두 표시합니다.
                if (normalizedType == normalizedExcludedType)
                    continue;

                visibleItems.Add(definition);
            }

            visibleItems.Sort(CompareItems);

            int shownCount = 0;

            foreach (ItemDefinition definition in visibleItems)
            {
                ItemDefinition capturedDefinition = definition;

                Sprite icon =
                    database.GetIconByKey(
                        capturedDefinition.Key
                    );

                if (icon == null && logMissingIcons)
                {
                    Debug.LogWarning(
                        $"[ExpendablesPageUI] 아이콘을 찾지 못했습니다. " +
                        $"key={capturedDefinition.Key}, " +
                        $"name={capturedDefinition.name}",
                        this
                    );
                }

                int ownedCount = showOwnedCount
                    ? GetOwnedItemCount(
                        capturedDefinition.id
                    )
                    : 0;

                ExpendableSlotUI slotView =
                    Instantiate(
                        itemSlotPrefab,
                        itemGridContent
                    );

                slotView.gameObject.name =
                    $"ExpendableSlot_" +
                    $"{capturedDefinition.Key}_" +
                    $"{capturedDefinition.name}";

                slotView.gameObject.SetActive(true);

                slotView.SetupExpendable(
                    capturedDefinition,
                    ownedCount,
                    icon,
                    () => HandleItemClicked(
                        capturedDefinition
                    )
                );

                spawnedSlots.Add(slotView.gameObject);
                shownCount++;
            }

            RefreshScrollbar(shownCount);

            if (logActions)
            {
                Debug.Log(
                    $"[ExpendablesPageUI] 목록 생성 완료. " +
                    $"allItems={allItems.Count}, " +
                    $"shownCount={shownCount}, " +
                    $"excludedType={normalizedExcludedType}",
                    this
                );
            }
        }

        /// <summary>
        /// 지금은 이벤트만 전달합니다.
        /// 아이템 사용이나 저장 처리는 하지 않습니다.
        /// </summary>
        private void HandleItemClicked(
            ItemDefinition definition)
        {
            if (definition == null)
                return;

            if (logActions)
            {
                Debug.Log(
                    $"[ExpendablesPageUI] Item clicked. " +
                    $"key={definition.Key}, " +
                    $"name={definition.name}, " +
                    $"type={definition.type}",
                    this
                );
            }

            ItemClicked?.Invoke(definition);
        }

        private int GetOwnedItemCount(string rawId)
        {
            StatusSaveFile saveFile =
                StatusSaveFile.Instance;

            if (saveFile == null ||
                saveFile.Data == null ||
                saveFile.Data.inventory == null ||
                saveFile.Data.inventory.item == null)
            {
                return 0;
            }

            rawId = GameDatabase.NormalizeId(rawId);

            if (rawId == GameDatabase.NoneKey)
                return 0;

            int total = 0;

            foreach (Dictionary<string, int> map
                     in saveFile.Data.inventory.item)
            {
                if (map == null)
                    continue;

                if (map.TryGetValue(
                        rawId,
                        out int count))
                {
                    total += Mathf.Max(0, count);
                }
            }

            return total;
        }

        private static int CompareItems(
            ItemDefinition left,
            ItemDefinition right)
        {
            if (left == null && right == null)
                return 0;

            if (left == null)
                return 1;

            if (right == null)
                return -1;

            int rankCompare =
                left.rank.CompareTo(right.rank);

            if (rankCompare != 0)
                return rankCompare;

            return string.CompareOrdinal(
                left.id,
                right.id
            );
        }

        private void AutoBindReferences()
        {
            if (database == null)
                database = GameDatabase.Instance;

            if (itemGridContent == null)
            {
                Transform target =
                    FindChildRecursive(
                        transform,
                        "ExpendableItemGridContent"
                    );

                if (target != null)
                    itemGridContent = target;
            }

            if (itemSlotPrefab == null)
            {
                Transform target =
                    FindChildRecursive(
                        transform,
                        "ExpendableItemSlot"
                    );

                if (target != null)
                {
                    itemSlotPrefab =
                        target.GetComponent<
                            ExpendableSlotUI
                        >();
                }
            }

            if (verticalScrollbar == null)
            {
                Transform target =
                    FindChildRecursive(
                        transform,
                        "Scrollbar Vertical"
                    );

                if (target != null)
                {
                    verticalScrollbar =
                        target.GetComponent<Scrollbar>();
                }
            }
        }

        private void HideSceneTemplate()
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

        private void ClearSpawnedSlots()
        {
            for (int i = spawnedSlots.Count - 1;
                 i >= 0;
                 i--)
            {
                if (spawnedSlots[i] != null)
                    Destroy(spawnedSlots[i]);
            }

            spawnedSlots.Clear();
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
    }
}