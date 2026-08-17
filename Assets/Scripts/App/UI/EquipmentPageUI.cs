using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dungeon.Core;

namespace Dungeon.UI
{
    /// <summary>
    /// Equipment 탭 전체 UI를 관리합니다.
    ///
    /// 보유 장비 목록은 StatusSaveFile.Instance.Data.inventory.equipment에서 읽고,
    /// 장착 정보는 StatusSaveFile.Instance.Data.equips.equip에 반영합니다.
    /// savefile에는 raw id("000")만 저장하고, UI/DB 조회에는 "equip:000" category key를 사용합니다.
    /// </summary>
    public class EquipmentPageUI : MonoBehaviour
    {
        [Header("Database")]
        [SerializeField] private GameDatabase database;

        [Header("Save File")]
        [Tooltip("StatusSaveFile이 없을 때 fallback으로 찾을 파일명입니다. 일반 플레이 중에는 StatusSaveFile.Instance가 우선됩니다.")]
        [SerializeField] private string saveFileName = "savefile_dev_runtime.json";

        [Tooltip("비워두면 StatusSaveFile.Instance 또는 기본 후보 경로를 사용합니다.")]
        [SerializeField] private string saveFilePathOverride = "";

        [Header("Equipped Slots")]
        [SerializeField] private EquipmentPartSlotUI headSlot = new EquipmentPartSlotUI { slotKey = "head", slotButtonName = "HeadSlotButton" };
        [SerializeField] private EquipmentPartSlotUI bodySlot = new EquipmentPartSlotUI { slotKey = "body", slotButtonName = "BodySlotButton" };
        [SerializeField] private EquipmentPartSlotUI legsSlot = new EquipmentPartSlotUI { slotKey = "legs", slotButtonName = "LegsSlotButton" };

        [Header("Inventory List")]
        [SerializeField] private Transform itemGridContent;
        [SerializeField] private EquipmentItemSlotUI itemSlotPrefab;
        [SerializeField] private Scrollbar verticalScrollbar;

        [Tooltip("Grid Layout Group의 Constraint Count와 같은 값으로 맞추세요.")]
        [SerializeField] private int gridColumnCount = 6;

        [Tooltip("이 줄 수를 초과하면 스크롤바를 켭니다. 2면 3줄부터 스크롤바 On.")]
        [SerializeField] private int visibleRowsBeforeScrollbar = 2;

        [Tooltip("페이지가 열렸을 때 처음에는 모든 보유 장비를 보여줍니다.")]
        [SerializeField] private bool showAllEquipmentOnOpen = true;

        [Header("Selection Visual")]
        [Tooltip("선택되지 않은 장비 슬롯의 SlotFrameOverlay 색상입니다.")]
        [SerializeField] private Color normalSlotFrameColor = Color.black;

        [Tooltip("선택된 장비 슬롯의 SlotFrameOverlay 색상입니다.")]
        [SerializeField] private Color selectedSlotFrameColor = Color.red;

        [Header("Debug")]
        [SerializeField] private bool logEquipmentActions = true;
        [SerializeField] private bool logMissingIcons = true;

        private readonly EquipmentSaveRepository saveRepository = new();
        private readonly List<GameObject> spawnedItemSlots = new();

        private string selectedSlotKey;
        private bool slotButtonsBound;

        private void Awake()
        {
            AutoBindReferences();
            BindSlotButtons();
            HideSceneTemplateIfNeeded();
        }

        private void OnEnable()
        {
            HideSceneTemplateIfNeeded();
            ReloadFromSave();
        }

        private void OnDisable()
        {
            selectedSlotKey = null;
            RefreshSlotSelectionVisuals();
        }

        public void ReloadFromSave()
        {
            AutoBindReferences();
            BindSlotButtons();
            HideSceneTemplateIfNeeded();

            if (database == null)
                database = GameDatabase.Instance;

            if (database == null)
            {
                Debug.LogWarning("[EquipmentPageUI] GameDatabase가 없습니다. 씬에 GameDatabase 오브젝트를 추가하세요.");
                return;
            }

            if (!saveRepository.Load(saveFileName, saveFilePathOverride))
            {
                Debug.LogWarning($"[EquipmentPageUI] savefile 로드 실패: {saveRepository.LastError}");
                RefreshEquippedSlotsOnly();
                ClearItemList();
                return;
            }

            if (logEquipmentActions)
            {
                Debug.Log($"[EquipmentPageUI] Loaded equipment save source: {saveRepository.CurrentPath}");
            }

            RefreshEquippedSlotsOnly();

            if (showAllEquipmentOnOpen)
                selectedSlotKey = null;

            RefreshSlotSelectionVisuals();

            if (showAllEquipmentOnOpen)
                PopulateInventoryList(null);
        }

        private void AutoBindReferences()
        {
            if (database == null)
                database = GameDatabase.Instance;

            AutoBindPartSlot(headSlot);
            AutoBindPartSlot(bodySlot);
            AutoBindPartSlot(legsSlot);

            if (itemGridContent == null)
            {
                Transform t = FindChildRecursive(transform, "ItemGridContent");
                if (t != null) itemGridContent = t;
            }

            if (verticalScrollbar == null)
            {
                Transform t = FindChildRecursive(transform, "Scrollbar Vertical");
                if (t != null) verticalScrollbar = t.GetComponent<Scrollbar>();
            }

            if (itemSlotPrefab == null)
            {
                Transform t = FindChildRecursive(transform, "EquipmentSlot");
                if (t != null) itemSlotPrefab = t.GetComponent<EquipmentItemSlotUI>();
            }
        }

        private void AutoBindPartSlot(EquipmentPartSlotUI slot)
        {
            if (slot == null || string.IsNullOrWhiteSpace(slot.slotButtonName))
                return;

            if (slot.slotButton == null)
            {
                Transform buttonTransform = FindChildRecursive(transform, slot.slotButtonName);
                if (buttonTransform != null)
                    slot.slotButton = buttonTransform.GetComponent<Button>();
            }

            Transform root = slot.slotButton != null ? slot.slotButton.transform : null;
            if (root == null) return;

            if (slot.equipItemIcon == null)
            {
                Transform t = FindChildRecursive(root, "EquipItemIcon");
                if (t != null) slot.equipItemIcon = t.GetComponent<Image>();
            }

            if (slot.emptyPlusIcon == null)
            {
                Transform t = FindChildRecursive(root, "EmptyPlusIcon");
                if (t != null) slot.emptyPlusIcon = t.gameObject;
            }

            if (slot.slotFrameOverlay == null)
            {
                Transform t = FindChildRecursive(root, "SlotFrameOverlay");
                if (t != null) slot.slotFrameOverlay = t.gameObject;
            }
        }

        private void HideSceneTemplateIfNeeded()
        {
            if (itemSlotPrefab == null || itemGridContent == null)
                return;

            if (itemSlotPrefab.transform == itemGridContent || itemSlotPrefab.transform.IsChildOf(itemGridContent))
                itemSlotPrefab.gameObject.SetActive(false);
        }

        private void BindSlotButtons()
        {
            if (slotButtonsBound)
                return;

            bool boundAny = false;
            boundAny |= BindSlotButton(headSlot);
            boundAny |= BindSlotButton(bodySlot);
            boundAny |= BindSlotButton(legsSlot);

            slotButtonsBound = boundAny;
        }

        private bool BindSlotButton(EquipmentPartSlotUI slot)
        {
            if (slot == null || slot.slotButton == null)
                return false;

            string key = GameDatabase.NormalizeSlot(slot.slotKey);

            slot.slotButton.onClick.RemoveAllListeners();
            slot.slotButton.interactable = true;

            SlotPointerClickRelay relay =
                slot.GetOrCreatePointerClickRelay();

            if (relay != null)
            {
                relay.LeftClicked +=
                    () => OnPartSlotLeftClicked(key);
                relay.RightClicked +=
                    () => OnPartSlotRightClicked(key);
            }

            return true;
        }

        private void OnPartSlotLeftClicked(string slotKey)
        {
            string normalizedSlotKey = GameDatabase.NormalizeSlot(slotKey);

            if (saveRepository.Load(saveFileName, saveFilePathOverride))
            {
                string equippedKey = saveRepository.GetEquippedKey(normalizedSlotKey);

                if (!string.IsNullOrWhiteSpace(equippedKey) &&
                    equippedKey != GameDatabase.NoneKey)
                {
                    ItemEquipmentInfoPopupUI.TryShow(equippedKey);
                    return;
                }
            }

            // Empty part slots preserve the existing inventory filter behavior.
            OnPartSlotClicked(normalizedSlotKey);
        }

        private void OnPartSlotClicked(string slotKey)
        {
            string normalizedSlotKey =
                GameDatabase.NormalizeSlot(slotKey);

            // 현재 선택된 슬롯을 다시 좌클릭하면 필터를 해제하고
            // 전체 장비 목록으로 돌아갑니다.
            if (selectedSlotKey == normalizedSlotKey)
            {
                selectedSlotKey = null;
                RefreshSlotSelectionVisuals();
                PopulateInventoryList(null);

                if (logEquipmentActions)
                {
                    Debug.Log(
                        "[EquipmentPageUI] Slot filter cleared. " +
                        "Showing all equipment."
                    );
                }

                return;
            }

            selectedSlotKey = normalizedSlotKey;
            RefreshSlotSelectionVisuals();

            if (logEquipmentActions)
                Debug.Log($"[EquipmentPageUI] Part slot clicked: {selectedSlotKey}");

            PopulateInventoryList(selectedSlotKey);
        }

        /// <summary>
        /// 장비 슬롯을 우클릭하면 해당 부위의 장비를 해제합니다.
        /// 선택 중인 필터는 유지합니다.
        /// </summary>
        private void OnPartSlotRightClicked(string slotKey)
        {
            string normalizedSlotKey =
                GameDatabase.NormalizeSlot(slotKey);

            if (!saveRepository.Load(
                    saveFileName,
                    saveFilePathOverride))
            {
                Debug.LogWarning(
                    $"[EquipmentPageUI] 장비 해제용 save 로드 실패: " +
                    $"{saveRepository.LastError}"
                );

                return;
            }

            string equippedKey =
                saveRepository.GetEquippedKey(
                    normalizedSlotKey
                );

            if (string.IsNullOrWhiteSpace(equippedKey) ||
                equippedKey == GameDatabase.NoneKey)
            {
                return;
            }

            // SetEquippedKey는 equip:xxx 형식만 받으므로,
            // 해제할 때는 raw id를 받는 SetEquippedId를 사용합니다.
            if (!saveRepository.SetEquippedId(
                    normalizedSlotKey,
                    GameDatabase.NoneKey))
            {
                Debug.LogWarning(
                    $"[EquipmentPageUI] 장비 해제 실패: " +
                    $"{saveRepository.LastError}"
                );

                return;
            }

            if (!EquipmentStatCalculator.Recalculate(
                    saveRepository.Data,
                    database))
            {
                Debug.LogWarning(
                    "[EquipmentPageUI] 장비 해제 후 " +
                    "스탯 재계산에 실패했습니다."
                );
            }

            if (!saveRepository.Save())
            {
                Debug.LogWarning(
                    $"[EquipmentPageUI] 장비 해제 저장 실패: " +
                    $"{saveRepository.LastError}"
                );

                return;
            }

            saveRepository.Load(
                saveFileName,
                saveFilePathOverride
            );

            RefreshEquippedSlotsOnly();
            RefreshSlotSelectionVisuals();
            PopulateInventoryList(selectedSlotKey);

            if (logEquipmentActions)
            {
                Debug.Log(
                    $"[EquipmentPageUI] Unequipped. " +
                    $"slot={normalizedSlotKey}, " +
                    $"key={equippedKey}"
                );
            }
        }

        /// <summary>
        /// 현재 선택된 장비 슬롯만 SlotFrameOverlay를 빨간색으로 표시합니다.
        /// 선택되지 않은 슬롯은 각 이미지가 원래 가지고 있던 색으로 되돌립니다.
        /// </summary>
        private void RefreshSlotSelectionVisuals()
        {
            RefreshPartSlotSelection(headSlot);
            RefreshPartSlotSelection(bodySlot);
            RefreshPartSlotSelection(legsSlot);
        }

        private void RefreshPartSlotSelection(EquipmentPartSlotUI slot)
        {
            if (slot == null)
                return;

            string normalizedSlotKey =
                GameDatabase.NormalizeSlot(slot.slotKey);

            bool selected =
                !string.IsNullOrWhiteSpace(selectedSlotKey) &&
                normalizedSlotKey == selectedSlotKey;

            slot.SetSelected(
                selected,
                normalSlotFrameColor,
                selectedSlotFrameColor
            );
        }

        private void PopulateInventoryList(string slotFilter)
        {
            ClearItemList();
            HideSceneTemplateIfNeeded();

            if (database == null || itemGridContent == null || itemSlotPrefab == null)
            {
                Debug.LogWarning("[EquipmentPageUI] 목록 생성에 필요한 참조가 부족합니다. database/itemGridContent/itemSlotPrefab을 확인하세요.");
                return;
            }

            if (!saveRepository.Load(saveFileName, saveFilePathOverride))
            {
                Debug.LogWarning($"[EquipmentPageUI] 장비 목록 로드 실패: {saveRepository.LastError}");
                RefreshScrollbar(0);
                return;
            }

            slotFilter = GameDatabase.NormalizeSlot(slotFilter);
            List<OwnedDbStack> owned = saveRepository.GetOwnedEquipmentStacks();
            int shownCount = 0;

            foreach (var stack in owned)
            {
                if (stack.count <= 0)
                    continue;

                if (!database.TryGetEquipmentByKey(stack.key, out var definition))
                {
                    Debug.LogWarning($"[EquipmentPageUI] db.json에서 장비 key '{stack.key}'를 찾지 못했습니다.");
                    continue;
                }

                string definitionSlot = GameDatabase.NormalizeSlot(definition.slot);
                if (!string.IsNullOrWhiteSpace(slotFilter) && definitionSlot != slotFilter)
                    continue;

                EquipmentDefinition capturedDefinition = definition;
                OwnedDbStack capturedStack = stack;
                Sprite icon = database.GetIconByKey(capturedDefinition.Key);

                if (icon == null && logMissingIcons)
                {
                    Debug.LogWarning(
                        $"[EquipmentPageUI] 장비 아이콘을 찾지 못했습니다. " +
                        $"key={capturedDefinition.Key}, id={capturedDefinition.id}, slot={capturedDefinition.slot}, name={capturedDefinition.name}. " +
                        $"GameDatabase의 Equipment Icon Resource Root 또는 Icon Overrides를 확인하세요."
                    );
                }

                string equippedKey =
                    saveRepository.GetEquippedKey(
                        definitionSlot
                    );

                bool isEquipped =
                    GameDatabase.NormalizeKey(equippedKey) ==
                    GameDatabase.NormalizeKey(
                        capturedDefinition.Key
                    );

                EquipmentItemSlotUI slotView = Instantiate(itemSlotPrefab, itemGridContent);
                slotView.gameObject.name = $"EquipmentSlot_{capturedDefinition.Key}_{definitionSlot}_{capturedDefinition.name}";
                slotView.gameObject.SetActive(true);
                slotView.SetupEquipment(
                    capturedDefinition,
                    capturedStack.count,
                    icon,
                    isEquipped,
                    () => OnInventoryEquipmentClicked(capturedDefinition),
                    () => OnInventoryEquipmentRightClicked(capturedDefinition)
                );

                spawnedItemSlots.Add(slotView.gameObject);
                shownCount++;
            }

            if (logEquipmentActions)
            {
                Debug.Log(
                    $"[EquipmentPageUI] PopulateInventoryList. " +
                    $"slotFilter={(string.IsNullOrWhiteSpace(slotFilter) ? "all" : slotFilter)}, " +
                    $"ownedCount={owned.Count}, shownCount={shownCount}"
                );
            }

            RefreshScrollbar(shownCount);
        }

        private void OnInventoryEquipmentClicked(EquipmentDefinition definition)
        {
            if (definition == null)
                return;

            string targetSlot = string.IsNullOrWhiteSpace(selectedSlotKey)
                ? GameDatabase.NormalizeSlot(definition.slot)
                : GameDatabase.NormalizeSlot(selectedSlotKey);

            string definitionSlot = GameDatabase.NormalizeSlot(definition.slot);
            if (definitionSlot != targetSlot)
            {
                Debug.LogWarning($"[EquipmentPageUI] '{definition.name}'은 '{definition.slot}' 장비라서 '{targetSlot}' 슬롯에 장착할 수 없습니다.");
                return;
            }

            // 여기서부터는 파일을 직접 고치는 것이 아니라 StatusSaveFile의 메모리 데이터를 수정합니다.
            if (!saveRepository.SetEquippedKey(targetSlot, definition.Key))
            {
                Debug.LogWarning($"[EquipmentPageUI] 장착 정보 변경 실패: {saveRepository.LastError}");
                return;
            }

            // 장착 정보가 바뀐 직후, DB의 장비 스탯 보너스를 반영해서 최종 스탯을 다시 계산합니다.
            // 기준 스탯에서 매번 재계산하므로 장비 보너스가 중복 누적되지 않습니다.
            if (!EquipmentStatCalculator.Recalculate(saveRepository.Data, database))
            {
                Debug.LogWarning("[EquipmentPageUI] 장비 스탯 재계산에 실패했습니다.");
            }

            if (!saveRepository.Save())
            {
                Debug.LogWarning($"[EquipmentPageUI] savefile 저장 실패: {saveRepository.LastError}");
                return;
            }

            if (logEquipmentActions)
            {
                Debug.Log($"[EquipmentPageUI] Equipped. slot={targetSlot}, key={definition.Key}, id={definition.id}, name={definition.name}");
            }

            // StatusSaveFile.Save()로 저장한 뒤 다시 읽어, 장착 슬롯과 목록을 최신 상태로 맞춥니다.
            saveRepository.Load(saveFileName, saveFilePathOverride);
            RefreshEquippedSlotsOnly();
            PopulateInventoryList(selectedSlotKey);
        }

        private void OnInventoryEquipmentRightClicked(
            EquipmentDefinition definition)
        {
            if (definition == null ||
                !saveRepository.Load(saveFileName, saveFilePathOverride))
            {
                return;
            }

            string slotKey = GameDatabase.NormalizeSlot(definition.slot);
            string equippedKey = GameDatabase.NormalizeKey(
                saveRepository.GetEquippedKey(slotKey)
            );

            if (equippedKey != GameDatabase.NormalizeKey(definition.Key))
                return;

            OnPartSlotRightClicked(slotKey);
        }

        private void RefreshEquippedSlotsOnly()
        {
            if (!saveRepository.Load(saveFileName, saveFilePathOverride))
            {
                if (logEquipmentActions)
                    Debug.LogWarning($"[EquipmentPageUI] 장착 슬롯 갱신용 save 로드 실패: {saveRepository.LastError}");
            }

            RefreshPartSlot(headSlot);
            RefreshPartSlot(bodySlot);
            RefreshPartSlot(legsSlot);
        }

        private void RefreshPartSlot(EquipmentPartSlotUI slot)
        {
            if (slot == null)
                return;

            string equippedKey = saveRepository.GetEquippedKey(slot.slotKey);
            bool hasEquip = !string.IsNullOrWhiteSpace(equippedKey) && equippedKey != GameDatabase.NoneKey;

            Sprite icon = null;
            if (hasEquip && database != null)
                icon = database.GetIconByKey(equippedKey);

            slot.SetEquippedState(hasEquip, icon);
        }

        private void ClearItemList()
        {
            for (int i = spawnedItemSlots.Count - 1; i >= 0; i--)
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

            int columns = Mathf.Max(1, gridColumnCount);
            int visibleRows = Mathf.Max(1, visibleRowsBeforeScrollbar);
            bool needScrollbar = shownCount > columns * visibleRows;

            verticalScrollbar.gameObject.SetActive(needScrollbar);
        }

        private static Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrWhiteSpace(targetName))
                return null;

            if (root.name == targetName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), targetName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }

    [Serializable]
    public class EquipmentPartSlotUI
    {
        [Tooltip("head/body/legs 중 하나입니다.")]
        public string slotKey = "head";

        [Tooltip("자동 연결에 사용할 SlotButton 오브젝트 이름입니다.")]
        public string slotButtonName = "HeadSlotButton";

        public Button slotButton;
        public Image equipItemIcon;
        public GameObject emptyPlusIcon;
        public GameObject slotFrameOverlay;

        [NonSerialized] private Image slotFrameImage;
        [NonSerialized] private SlotPointerClickRelay pointerClickRelay;

        public void SetEquippedState(bool equipped, Sprite icon)
        {
            if (equipItemIcon != null)
            {
                equipItemIcon.gameObject.SetActive(equipped);
                equipItemIcon.sprite = icon;
                equipItemIcon.enabled = equipped && icon != null;
                equipItemIcon.preserveAspect = true;
                equipItemIcon.raycastTarget = false;
            }

            if (emptyPlusIcon != null)
                emptyPlusIcon.SetActive(!equipped);

            if (slotFrameOverlay != null)
                slotFrameOverlay.SetActive(true);
        }

        /// <summary>
        /// SlotButton 자식의 SlotFrameOverlay Image 색만 변경합니다.
        /// 오브젝트 활성 상태는 바꾸지 않습니다.
        ///
        /// 주의: Image.color 틴트가 보이려면 원본 테두리 스프라이트가
        /// 검정색이 아니라 흰색으로 제작되어 있어야 합니다.
        /// </summary>
        public void SetSelected(
            bool selected,
            Color normalColor,
            Color selectedColor)
        {
            Image frameImage = GetSlotFrameImage();

            if (frameImage == null)
                return;

            frameImage.color = selected
                ? selectedColor
                : normalColor;
        }

        public SlotPointerClickRelay GetOrCreatePointerClickRelay()
        {
            if (pointerClickRelay != null)
                return pointerClickRelay;

            if (slotButton == null)
                return null;

            pointerClickRelay =
                slotButton.GetComponent<SlotPointerClickRelay>();

            if (pointerClickRelay == null)
            {
                pointerClickRelay =
                    slotButton.gameObject.AddComponent<
                        SlotPointerClickRelay
                    >();
            }

            return pointerClickRelay;
        }

        private Image GetSlotFrameImage()
        {
            if (slotFrameImage != null)
                return slotFrameImage;

            if (slotFrameOverlay == null)
                return null;

            // SlotFrameOverlay 오브젝트 자체의 Image만 사용합니다.
            // 다른 자식 이미지(numberImage 등)는 검색하지 않습니다.
            slotFrameImage =
                slotFrameOverlay.GetComponent<Image>();

            return slotFrameImage;
        }
    }
}
