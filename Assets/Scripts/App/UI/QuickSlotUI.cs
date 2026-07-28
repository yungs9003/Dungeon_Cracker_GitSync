using System;
using UnityEngine;
using UnityEngine.UI;
using Dungeon.Core;

namespace Dungeon.UI
{
    /// <summary>
    /// 아이템창 퀵 슬롯과 게임 HUD 퀵 슬롯이 공통으로 사용하는 UI입니다.
    ///
    /// 같은 Slot Number를 가진 QuickSlotUI는
    /// StatusSaveFile의 동일한 equips.item.slotN 값을 표시합니다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class QuickSlotUI : MonoBehaviour
    {
        [Header("Slot")]
        [SerializeField, Range(1, 5)]
        private int slotNumber = 1;

        [Header("Database")]
        [SerializeField] private GameDatabase database;

        [Header("UI References")]
        [SerializeField] private Button slotButton;
        [SerializeField] private Image itemIcon;
        [SerializeField] private GameObject emptyPlusImage;
        [SerializeField] private GameObject selectedFrame;

        [Header("Debug")]
        [SerializeField] private bool logActions;
        [SerializeField] private bool logMissingIcons = true;

        private StatusSaveFile subscribedSaveFile;

        public int SlotNumber => slotNumber;

        public string CurrentItemKey { get; private set; }
            = GameDatabase.NoneKey;

        public bool HasItem =>
            !string.IsNullOrWhiteSpace(CurrentItemKey) &&
            CurrentItemKey != GameDatabase.NoneKey;

        /// <summary>
        /// 아이템창에서는 등록할 슬롯 선택,
        /// HUD에서는 아이템 사용 요청에 이용할 수 있습니다.
        /// </summary>
        public event Action<QuickSlotUI> Clicked;

        private void Awake()
        {
            AutoBindReferences();
            BindButton();
        }

        private void Start()
        {
            // 모든 오브젝트의 Awake가 끝난 후 다시 연결합니다.
            SubscribeToSaveFile();
            RefreshFromSave();
        }

        private void OnEnable()
        {
            AutoBindReferences();
            SubscribeToSaveFile();
            RefreshFromSave();
        }

        private void OnDisable()
        {
            UnsubscribeFromSaveFile();
        }

        private void OnDestroy()
        {
            UnsubscribeFromSaveFile();

            if (slotButton != null)
                slotButton.onClick.RemoveListener(HandleClicked);
        }

        /// <summary>
        /// 이 슬롯에 소비 아이템을 등록하고 저장합니다.
        /// itemKey는 item:000 또는 000 형식을 받을 수 있습니다.
        /// </summary>
        public bool RegisterItem(string itemKey)
        {
            AutoBindReferences();

            if (database == null)
                database = GameDatabase.Instance;

            string normalizedKey = NormalizeItemKey(itemKey);

            if (normalizedKey == GameDatabase.NoneKey)
            {
                Debug.LogWarning(
                    $"[QuickSlotUI] 등록할 아이템 key가 비어 있습니다. " +
                    $"slot={slotNumber}"
                );

                return false;
            }

            if (database == null)
            {
                Debug.LogWarning(
                    "[QuickSlotUI] GameDatabase를 찾지 못했습니다."
                );

                return false;
            }

            if (!database.TryGetItemByKey(
                    normalizedKey,
                    out ItemDefinition definition))
            {
                Debug.LogWarning(
                    $"[QuickSlotUI] db.json에서 아이템을 찾지 못했습니다: " +
                    $"{normalizedKey}"
                );

                return false;
            }

            if (!string.Equals(
                    definition.type,
                    "consumable",
                    StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning(
                    $"[QuickSlotUI] 소비 아이템만 등록할 수 있습니다. " +
                    $"key={normalizedKey}, " +
                    $"type={definition.type}"
                );

                return false;
            }

            StatusSaveFile saveFile = GetSaveFile();

            if (saveFile == null)
            {
                Debug.LogWarning(
                    "[QuickSlotUI] StatusSaveFile을 찾지 못했습니다."
                );

                return false;
            }

            bool saved = saveFile.SetQuickSlotItemKey(
                slotNumber,
                normalizedKey
            );

            if (!saved)
                return false;

            // 저장 이벤트 구독 상태와 관계없이 클릭한 슬롯은 즉시 갱신합니다.
            RefreshFromSave();

            if (logActions)
            {
                Debug.Log(
                    $"[QuickSlotUI] Item registered. " +
                    $"slot={slotNumber}, " +
                    $"key={normalizedKey}, " +
                    $"name={definition.name}"
                );
            }

            return true;
        }

        /// <summary>
        /// 현재 퀵 슬롯 등록을 해제합니다.
        /// </summary>
        public bool ClearItem()
        {
            StatusSaveFile saveFile = GetSaveFile();

            if (saveFile == null)
                return false;

            bool cleared =
                saveFile.ClearQuickSlotItem(slotNumber);

            if (cleared)
                RefreshFromSave();

            return cleared;
        }

        /// <summary>
        /// 저장된 슬롯 정보를 다시 읽고 아이콘을 표시합니다.
        /// </summary>
        public void RefreshFromSave()
        {
            AutoBindReferences();

            if (database == null)
                database = GameDatabase.Instance;

            StatusSaveFile saveFile = GetSaveFile();

            if (saveFile == null)
            {
                ApplyVisual(GameDatabase.NoneKey, null);
                return;
            }

            string itemKey =
                saveFile.GetQuickSlotItemKey(slotNumber);

            Sprite icon = null;

            if (itemKey != GameDatabase.NoneKey &&
                database != null)
            {
                icon = database.GetIconByKey(itemKey);
            }

            ApplyVisual(itemKey, icon);
        }

        public void SetSelected(bool selected)
        {
            if (selectedFrame != null)
                selectedFrame.SetActive(selected);
        }

        private void ApplyVisual(string itemKey, Sprite icon)
        {
            CurrentItemKey = string.IsNullOrWhiteSpace(itemKey)
                ? GameDatabase.NoneKey
                : GameDatabase.NormalizeKey(itemKey);

            bool hasItem =
                CurrentItemKey != GameDatabase.NoneKey;

            bool hasIcon =
                hasItem && icon != null;

            if (hasItem && icon == null && logMissingIcons)
            {
                Debug.LogWarning(
                    $"[QuickSlotUI] 등록된 아이템의 아이콘을 찾지 못했습니다. " +
                    $"slot={slotNumber}, key={CurrentItemKey}",
                    this
                );
            }

            if (itemIcon != null)
            {
                itemIcon.sprite = icon;
                itemIcon.enabled = hasIcon;
                itemIcon.gameObject.SetActive(hasIcon);
                itemIcon.preserveAspect = true;
                itemIcon.raycastTarget = false;
            }

            // HUD 슬롯에서는 emptyPlusImage를 연결하지 않아도 됩니다.
            // 아이템은 등록됐지만 아이콘만 없는 경우에는 빈 슬롯으로 오인하지 않도록 +를 숨깁니다.
            if (emptyPlusImage != null)
                emptyPlusImage.SetActive(!hasItem);
        }

        private void HandleClicked()
        {
            Clicked?.Invoke(this);
        }

        private void HandleSaveDataChanged(StatusSaveData _)
        {
            // 어느 QuickSlotUI가 저장했는지는 상관없이
            // 모든 슬롯이 자신의 slotNumber를 다시 읽습니다.
            RefreshFromSave();
        }

        private void BindButton()
        {
            if (slotButton == null)
                return;

            EnsureButtonGraphic();

            slotButton.onClick.RemoveListener(HandleClicked);
            slotButton.onClick.AddListener(HandleClicked);
        }

        private void SubscribeToSaveFile()
        {
            StatusSaveFile saveFile = GetSaveFile();

            if (saveFile == subscribedSaveFile)
                return;

            UnsubscribeFromSaveFile();

            subscribedSaveFile = saveFile;

            if (subscribedSaveFile != null)
            {
                subscribedSaveFile.OnSaveDataChanged +=
                    HandleSaveDataChanged;
            }
        }

        private void UnsubscribeFromSaveFile()
        {
            if (subscribedSaveFile != null)
            {
                subscribedSaveFile.OnSaveDataChanged -=
                    HandleSaveDataChanged;
            }

            subscribedSaveFile = null;
        }

        private StatusSaveFile GetSaveFile()
        {
            if (StatusSaveFile.Instance != null)
                return StatusSaveFile.Instance;

            return FindFirstObjectByType<StatusSaveFile>();
        }

        private string NormalizeItemKey(string itemKey)
        {
            if (string.IsNullOrWhiteSpace(itemKey) ||
                itemKey == GameDatabase.NoneKey)
            {
                return GameDatabase.NoneKey;
            }

            string normalized =
                GameDatabase.NormalizeKey(itemKey);

            if (GameDatabase.TryParseKey(
                    normalized,
                    out string category,
                    out string rawId))
            {
                if (category != GameDatabase.CategoryItem)
                    return GameDatabase.NoneKey;

                return GameDatabase.MakeKey(
                    GameDatabase.CategoryItem,
                    rawId
                );
            }

            // 000처럼 raw ID가 전달된 경우입니다.
            return GameDatabase.MakeKey(
                GameDatabase.CategoryItem,
                normalized
            );
        }

        private void AutoBindReferences()
        {
            if (database == null)
                database = GameDatabase.Instance;

            if (slotButton == null)
                slotButton = GetComponent<Button>();

            if (itemIcon == null)
                itemIcon = FindChildImage("ItemIcon");

            if (emptyPlusImage == null)
            {
                Transform target =
                    FindChildRecursive(
                        transform,
                        "EmptyPlusImage"
                    );

                if (target != null)
                    emptyPlusImage = target.gameObject;
            }

            if (selectedFrame == null)
            {
                Transform target =
                    FindChildRecursive(
                        transform,
                        "SelectedFrame"
                    );

                if (target != null)
                    selectedFrame = target.gameObject;
            }
        }

        private Image FindChildImage(string targetName)
        {
            Image[] images =
                GetComponentsInChildren<Image>(true);

            foreach (Image image in images)
            {
                if (image != null &&
                    image.transform != transform &&
                    image.name == targetName)
                {
                    return image;
                }
            }

            return null;
        }

        private void EnsureButtonGraphic()
        {
            if (slotButton == null ||
                slotButton.targetGraphic != null)
            {
                return;
            }

            Image rootImage = GetComponent<Image>();

            if (rootImage == null)
            {
                rootImage = gameObject.AddComponent<Image>();

                Color color = rootImage.color;
                color.a = 0f;
                rootImage.color = color;
            }

            rootImage.raycastTarget = true;
            slotButton.targetGraphic = rootImage;
        }

        private static Transform FindChildRecursive(
            Transform root,
            string targetName)
        {
            if (root == null)
                return null;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);

                if (child.name == targetName)
                    return child;

                Transform result =
                    FindChildRecursive(child, targetName);

                if (result != null)
                    return result;
            }

            return null;
        }
    }
}