using System.Collections.Generic;
using Dungeon.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dungeon.UI
{
    /// <summary>
    /// Module 씬의 보유 목록, 6개 장착 슬롯, 상세 정보와 저장을 연결합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModulePageUI : MonoBehaviour
    {
        [Header("Database")]
        [SerializeField] private GameDatabase database;

        [Header("Module Details")]
        [SerializeField] private GameObject infoContent;
        [SerializeField] private Image moduleIcon;
        [SerializeField] private TMP_Text moduleNameText;
        [SerializeField] private TMP_Text descriptionText;

        [Header("Slot Areas")]
        [SerializeField] private Transform inventoryArea;
        [SerializeField] private Transform equippedArea;

        [Header("Legacy Initial Selection")]
        [SerializeField] private bool showDefaultSelection = true;
        [SerializeField] private ModuleSlotUI defaultSelection;

        [Header("Debug")]
        [SerializeField] private bool logModuleActions = true;

        private readonly ModuleSaveRepository saveRepository = new();
        private readonly List<ModuleSlotUI> inventorySlots = new();
        private readonly List<ModuleSlotUI> equippedSlots = new();

        private ModuleSlotUI selectedSlot;
        private int selectedEquippedSlotIndex;

        public ModuleSlotUI SelectedSlot => selectedSlot;

        private void Awake()
        {
            AutoBindReferences();
            CollectSlots();
        }

        private void OnEnable()
        {
            ReloadFromSave();
        }

        public void ReloadFromSave()
        {
            AutoBindReferences();
            CollectSlots();

            if (database == null)
                database = GameDatabase.Instance;

            if (database == null)
            {
                Debug.LogWarning("[ModulePageUI] GameDatabase가 없습니다.");
                return;
            }

            if (!saveRepository.Load())
            {
                Debug.LogWarning($"[ModulePageUI] savefile 로드 실패: {saveRepository.LastError}");
                return;
            }

            RefreshInventorySlots();
            RefreshEquippedSlots();

            if (selectedSlot != null && selectedSlot.HasModule)
            {
                SelectSlot(selectedSlot);
            }
            else if (showDefaultSelection)
            {
                ModuleSlotUI first = FindFirstSelectableSlot();
                if (first != null)
                    SelectSlot(first);
                else
                    HideDetails();
            }
            else
            {
                HideDetails();
            }

            if (logModuleActions)
                Debug.Log($"[ModulePageUI] Loaded module save source: {saveRepository.CurrentPath}");
        }

        internal void HandleSingleLeftClick(ModuleSlotUI slot)
        {
            if (slot == null)
                return;

            if (slot.Role == ModuleSlotRole.Equipped)
            {
                selectedEquippedSlotIndex = slot.EquippedSlotIndex;
                RefreshEquippedTargetVisuals();

                if (!slot.HasModule)
                {
                    HideDetails();
                    slot.SetSelected(true);
                    return;
                }
            }

            if (!slot.HasModule)
                return;

            SelectSlot(slot);
        }

        internal void HandleDoubleLeftClick(ModuleSlotUI slot)
        {
            if (slot == null || !slot.HasModule)
                return;

            SelectSlot(slot);

            if (slot.Role != ModuleSlotRole.Inventory)
                return;

            if (!saveRepository.Load())
            {
                Debug.LogWarning($"[ModulePageUI] 장착용 savefile 로드 실패: {saveRepository.LastError}");
                return;
            }

            int existingSlot = saveRepository.FindEquippedSlot(slot.ModuleId);
            if (existingSlot > 0)
            {
                selectedEquippedSlotIndex = existingSlot;
                RefreshEquippedSlots();
                return;
            }

            int targetSlot = selectedEquippedSlotIndex > 0
                ? selectedEquippedSlotIndex
                : saveRepository.FindFirstEmptySlot();

            if (targetSlot <= 0)
            {
                Debug.LogWarning(
                    "[ModulePageUI] 빈 장착 슬롯이 없습니다. 교체할 장착 슬롯을 먼저 좌클릭하세요."
                );
                return;
            }

            if (!saveRepository.SetEquippedId(targetSlot, slot.ModuleId) ||
                !saveRepository.Save())
            {
                Debug.LogWarning($"[ModulePageUI] 모듈 장착 실패: {saveRepository.LastError}");
                return;
            }

            selectedEquippedSlotIndex = targetSlot;
            if (logModuleActions)
            {
                Debug.Log(
                    $"[ModulePageUI] Equipped. slot={targetSlot}, " +
                    $"key={slot.ModuleKey}, name={slot.ModuleName}"
                );
            }

            ReloadFromSave();
        }

        internal void HandleRightClick(ModuleSlotUI slot)
        {
            if (slot == null || !slot.HasModule || !saveRepository.Load())
                return;

            int targetSlot = slot.Role == ModuleSlotRole.Equipped
                ? slot.EquippedSlotIndex
                : saveRepository.FindEquippedSlot(slot.ModuleId);

            if (targetSlot <= 0)
                return;

            string removedKey = saveRepository.GetEquippedKey(targetSlot);
            if (!saveRepository.ClearSlot(targetSlot) || !saveRepository.Save())
            {
                Debug.LogWarning($"[ModulePageUI] 모듈 해제 실패: {saveRepository.LastError}");
                return;
            }

            if (selectedEquippedSlotIndex == targetSlot)
                selectedEquippedSlotIndex = 0;

            if (logModuleActions)
                Debug.Log($"[ModulePageUI] Unequipped. slot={targetSlot}, key={removedKey}");

            ReloadFromSave();
        }

        public void ShowDetails(
            ModuleSlotUI sourceSlot,
            Sprite icon,
            string moduleName,
            string description)
        {
            if (selectedSlot != sourceSlot)
            {
                if (selectedSlot != null)
                    selectedSlot.SetSelected(false);

                selectedSlot = sourceSlot;
            }

            if (selectedSlot != null)
                selectedSlot.SetSelected(true);

            ApplyDetails(icon, moduleName, description);
        }

        public void ShowDetails(Sprite icon, string moduleName, string description)
        {
            if (selectedSlot != null)
            {
                selectedSlot.SetSelected(false);
                selectedSlot = null;
            }

            ApplyDetails(icon, moduleName, description);
        }

        public void HideDetails()
        {
            if (selectedSlot != null)
            {
                selectedSlot.SetSelected(false);
                selectedSlot = null;
            }

            if (infoContent != null)
                infoContent.SetActive(false);
        }

        public void Configure(
            GameObject content,
            Image icon,
            TMP_Text nameText,
            TMP_Text bodyText,
            ModuleSlotUI initialSelection,
            bool showInitialSelection)
        {
            infoContent = content;
            moduleIcon = icon;
            moduleNameText = nameText;
            descriptionText = bodyText;
            defaultSelection = initialSelection;
            showDefaultSelection = showInitialSelection;
        }

        private void AutoBindReferences()
        {
            if (database == null)
                database = GameDatabase.Instance;

            inventoryArea ??= FindChildRecursive(transform, "ModuleInventoryArea");
            equippedArea ??= FindChildRecursive(transform, "EquippedSlots");

            if (infoContent == null)
            {
                Transform found = FindChildRecursive(transform, "ModuleInfoArea");
                if (found != null)
                    infoContent = found.gameObject;
            }

            if (moduleIcon == null && infoContent != null)
            {
                Transform found = FindChildRecursive(infoContent.transform, "SelectedModuleIcon");
                if (found != null)
                    moduleIcon = found.GetComponent<Image>();
            }

            if (moduleNameText == null && infoContent != null)
            {
                Transform found = FindChildRecursive(infoContent.transform, "ModuleNameText");
                if (found != null)
                    moduleNameText = found.GetComponent<TMP_Text>();
            }

            if (descriptionText == null && infoContent != null)
            {
                Transform found = FindChildRecursive(infoContent.transform, "DescriptionText");
                if (found != null)
                    descriptionText = found.GetComponent<TMP_Text>();
            }
        }

        private void CollectSlots()
        {
            inventorySlots.Clear();
            equippedSlots.Clear();

            CollectDirectChildSlots(inventoryArea, inventorySlots);
            CollectDirectChildSlots(equippedArea, equippedSlots);

            inventorySlots.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        private static void CollectDirectChildSlots(
            Transform area,
            List<ModuleSlotUI> target)
        {
            if (area == null)
                return;

            for (int i = 0; i < area.childCount; i++)
            {
                ModuleSlotUI slot = area.GetChild(i).GetComponentInChildren<ModuleSlotUI>(true);
                if (slot != null && !target.Contains(slot))
                    target.Add(slot);
            }
        }

        private void RefreshInventorySlots()
        {
            List<OwnedDbStack> owned = saveRepository.GetOwnedModuleStacks();

            for (int i = 0; i < inventorySlots.Count; i++)
            {
                ModuleSlotUI view = inventorySlots[i];
                if (i >= owned.Count ||
                    !database.TryGetModuleByKey(owned[i].key, out ModuleDefinition definition))
                {
                    view.ConfigureRuntime(this, null, null, ModuleSlotRole.Inventory, 0);
                    view.gameObject.SetActive(false);
                    continue;
                }

                Sprite icon = database.GetModuleIcon(definition.id);
                view.gameObject.SetActive(true);
                view.enabled = true;
                view.ConfigureRuntime(this, definition, icon, ModuleSlotRole.Inventory, 0);
            }
        }

        private void RefreshEquippedSlots()
        {
            for (int i = 0; i < equippedSlots.Count; i++)
            {
                int slotNumber = i + 1;
                ModuleSlotUI view = equippedSlots[i];
                string key = saveRepository.GetEquippedKey(slotNumber);

                ModuleDefinition definition = null;
                Sprite icon = null;
                if (key != GameDatabase.NoneKey && database.TryGetModuleByKey(key, out definition))
                    icon = database.GetModuleIcon(definition.id);

                view.gameObject.SetActive(true);
                view.enabled = true;
                view.ConfigureRuntime(
                    this,
                    definition,
                    icon,
                    ModuleSlotRole.Equipped,
                    slotNumber
                );
                view.SetSelected(slotNumber == selectedEquippedSlotIndex);
            }
        }

        private void RefreshEquippedTargetVisuals()
        {
            for (int i = 0; i < equippedSlots.Count; i++)
            {
                ModuleSlotUI view = equippedSlots[i];
                if (view != null)
                    view.SetSelected(i + 1 == selectedEquippedSlotIndex);
            }
        }

        private ModuleSlotUI FindFirstSelectableSlot()
        {
            if (defaultSelection != null && defaultSelection.HasModule)
                return defaultSelection;

            foreach (ModuleSlotUI slot in inventorySlots)
            {
                if (slot != null && slot.HasModule)
                    return slot;
            }

            foreach (ModuleSlotUI slot in equippedSlots)
            {
                if (slot != null && slot.HasModule)
                    return slot;
            }

            return null;
        }

        private void SelectSlot(ModuleSlotUI slot)
        {
            if (slot == null || !slot.HasModule)
                return;

            ShowDetails(
                slot,
                slot.ModuleIcon,
                slot.ModuleName,
                slot.Description
            );
        }

        private void ApplyDetails(Sprite icon, string moduleName, string description)
        {
            if (infoContent != null)
                infoContent.SetActive(true);

            if (moduleIcon != null)
            {
                moduleIcon.sprite = icon;
                moduleIcon.enabled = icon != null;
            }

            if (moduleNameText != null)
                moduleNameText.text = moduleName ?? string.Empty;

            if (descriptionText != null)
                descriptionText.text = description ?? string.Empty;
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
}
