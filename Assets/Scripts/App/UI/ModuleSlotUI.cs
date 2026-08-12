using UnityEngine;
using UnityEngine.UI;

namespace Dungeon.UI
{
    /// <summary>
    /// 장착/보유 모듈 슬롯의 공통 시각 프리팹에 붙는 컴포넌트입니다.
    /// 모듈 데이터가 있는 보유 슬롯만 클릭 가능하며, 상세 표시는 ModulePageUI에 위임합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ModuleSlotUI : MonoBehaviour
    {
        [SerializeField] private ModulePageUI modulePage;
        [SerializeField] private Image moduleIconImage;
        [SerializeField] private Sprite moduleIcon;
        [SerializeField] private string moduleName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private bool selectable;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(Select);
            RefreshVisual();
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

            modulePage.ShowDetails(moduleIcon, moduleName, description);
        }

        public void Configure(
            ModulePageUI page,
            Image iconImage,
            Sprite icon,
            string title,
            string body,
            bool canSelect)
        {
            modulePage = page;
            moduleIconImage = iconImage;
            moduleIcon = icon;
            moduleName = title ?? string.Empty;
            description = body ?? string.Empty;
            selectable = canSelect && icon != null;

            button = GetComponent<Button>();
            RefreshVisual();
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
    }
}
