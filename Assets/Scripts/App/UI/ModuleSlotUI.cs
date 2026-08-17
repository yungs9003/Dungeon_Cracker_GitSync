using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dungeon.UI
{
    /// <summary>
    /// 장착/보유 모듈 슬롯의 공통 표시와 포인터 피드백을 담당합니다.
    /// 실제 장착·해제 및 저장은 별도 데이터 계층에서 연결하도록 분리되어 있습니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ModuleSlotUI : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
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

        public bool IsSelectable => selectable;
        public bool IsSelected => selected;
        public string ModuleName => moduleName;

        private void Awake()
        {
            baseScale = transform.localScale;
            button = GetComponent<Button>();
            button.onClick.AddListener(Select);
            RefreshVisual();
            RefreshInteractionVisual();
        }

        private void OnDisable()
        {
            hovered = false;
            pressed = false;
            RefreshInteractionVisual();
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(Select);
        }

        public void Select()
        {
            if (!selectable || moduleIcon == null || modulePage == null)
                return;

            modulePage.ShowDetails(this, moduleIcon, moduleName, description);
        }

        public void SetSelected(bool value)
        {
            selected = value && selectable;
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
            selectable = canSelect && icon != null;
            selectionHighlight = highlight;

            button = GetComponent<Button>();
            baseScale = transform.localScale;
            selected = false;
            RefreshVisual();
            RefreshInteractionVisual();
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

        private void RefreshVisual()
        {
            if (moduleIconImage != null)
            {
                moduleIconImage.sprite = moduleIcon;
                moduleIconImage.enabled = moduleIcon != null;
                moduleIconImage.gameObject.SetActive(moduleIcon != null);
            }

            if (button == null)
                button = GetComponent<Button>();

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
