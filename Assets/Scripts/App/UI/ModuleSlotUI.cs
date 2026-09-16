using Dungeon.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dungeon.UI
{
    public enum ModuleSlotRole
    {
        Inventory = 0,
        Equipped = 1
    }

    /// <summary>
    /// 모듈 슬롯의 공통 표시와 좌/더블/우클릭 입력을 담당합니다.
    /// 실제 장착 데이터 변경과 저장은 ModulePageUI가 수행합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ModuleSlotUI : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler
    {
        [SerializeField] private ModulePageUI modulePage;
        [SerializeField] private Image moduleIconImage;
        [SerializeField] private Sprite moduleIcon;
        [SerializeField] private string moduleName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private bool selectable;
        [SerializeField] private Behaviour selectionHighlight;

        private Button button;
        private Vector3 baseScale = Vector3.one;
        private bool selected;
        private bool hovered;
        private bool pressed;
        private bool baseScaleCaptured;
        private string moduleId = GameDatabase.NoneKey;
        private ModuleSlotRole role;
        private int equippedSlotIndex;

        public bool IsSelectable => selectable;
        public bool IsSelected => selected;
        public bool HasModule => moduleId != GameDatabase.NoneKey;
        public string ModuleId => moduleId;
        public string ModuleKey => HasModule
            ? GameDatabase.MakeKey(GameDatabase.CategoryModule, moduleId)
            : GameDatabase.NoneKey;
        public string ModuleName => moduleName;
        public string Description => description;
        public Sprite ModuleIcon => moduleIcon;
        public ModuleSlotRole Role => role;
        public int EquippedSlotIndex => equippedSlotIndex;

        private void Awake()
        {
            CaptureBaseScale();
            button = GetComponent<Button>();
            AutoBindVisuals();
            RefreshVisual();
            RefreshInteractionVisual();
        }

        private void OnDisable()
        {
            hovered = false;
            pressed = false;
            RefreshInteractionVisual();
        }

        public void Select()
        {
            if (modulePage != null)
                modulePage.HandleSingleLeftClick(this);
        }

        public void SetSelected(bool value)
        {
            selected = value && selectable;
            RefreshInteractionVisual();
        }

        public void ConfigureRuntime(
            ModulePageUI page,
            ModuleDefinition definition,
            Sprite icon,
            ModuleSlotRole slotRole,
            int slotIndex)
        {
            modulePage = page;
            role = slotRole;
            equippedSlotIndex = slotRole == ModuleSlotRole.Equipped ? slotIndex : 0;
            moduleId = definition != null
                ? GameDatabase.NormalizeId(definition.id)
                : GameDatabase.NoneKey;
            moduleIcon = icon;
            moduleName = definition?.name ?? string.Empty;
            description = definition?.desc ?? string.Empty;
            selectable = slotRole == ModuleSlotRole.Equipped || definition != null;

            selected = false;
            button = GetComponent<Button>();
            CaptureBaseScale();
            AutoBindVisuals();
            RefreshVisual();
            RefreshInteractionVisual();
        }

        public void Configure(
            ModulePageUI page,
            Image iconImage,
            Sprite icon,
            string title,
            string body,
            bool canSelect,
            Behaviour highlight = null)
        {
            modulePage = page;
            moduleIconImage = iconImage;
            moduleIcon = icon;
            moduleName = title ?? string.Empty;
            description = body ?? string.Empty;
            moduleId = icon != null ? name : GameDatabase.NoneKey;
            selectable = canSelect && icon != null;
            selectionHighlight = highlight;
            role = ModuleSlotRole.Inventory;
            equippedSlotIndex = 0;

            button = GetComponent<Button>();
            CaptureBaseScale();
            selected = false;
            AutoBindVisuals();
            RefreshVisual();
            RefreshInteractionVisual();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!selectable || modulePage == null)
                return;

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                modulePage.HandleRightClick(this);
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            if (eventData.clickCount >= 2)
                modulePage.HandleDoubleLeftClick(this);
            else
                modulePage.HandleSingleLeftClick(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!selectable)
                return;

            hovered = true;
            RefreshInteractionVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
            RefreshInteractionVisual();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!selectable || eventData.button != PointerEventData.InputButton.Left)
                return;

            pressed = true;
            RefreshInteractionVisual();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
            RefreshInteractionVisual();
        }

        private void AutoBindVisuals()
        {
            if (moduleIconImage == null)
            {
                Image[] images = GetComponentsInChildren<Image>(true);
                foreach (Image image in images)
                {
                    if (image.gameObject == gameObject)
                        continue;

                    if (image.name == "ModuleIcon")
                    {
                        moduleIconImage = image;
                        break;
                    }

                    moduleIconImage ??= image;
                }
            }

            if (selectionHighlight == null)
                selectionHighlight = GetComponent<Outline>();
        }

        private void CaptureBaseScale()
        {
            if (baseScaleCaptured)
                return;

            baseScale = transform.localScale;
            baseScaleCaptured = true;
        }

        private void RefreshVisual()
        {
            if (moduleIconImage != null)
            {
                moduleIconImage.sprite = moduleIcon;
                moduleIconImage.enabled = HasModule && moduleIcon != null;
                moduleIconImage.gameObject.SetActive(HasModule && moduleIcon != null);
                moduleIconImage.preserveAspect = true;
                moduleIconImage.raycastTarget = false;
            }

            button ??= GetComponent<Button>();
            if (button != null)
                button.interactable = selectable;
        }

        private void RefreshInteractionVisual()
        {
            if (selectionHighlight != null)
                selectionHighlight.enabled = selected;

            float scale = pressed ? 0.97f : hovered ? 1.025f : 1f;
            transform.localScale = baseScale * scale;
        }
    }
}
