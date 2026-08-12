using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dungeon.UI
{
    /// <summary>
    /// Module 씬의 상세 정보 영역을 관리합니다.
    /// 슬롯은 표시할 데이터를 이 컴포넌트에 전달하고, 상세 UI의 실제 갱신은 여기서만 처리합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModulePageUI : MonoBehaviour
    {
        [Header("Module Details")]
        [SerializeField] private GameObject infoContent;
        [SerializeField] private Image moduleIcon;
        [SerializeField] private TMP_Text moduleNameText;
        [SerializeField] private TMP_Text descriptionText;

        [Header("Initial Selection")]
        [SerializeField] private bool showDefaultSelection = true;
        [SerializeField] private ModuleSlotUI defaultSelection;

        private void Start()
        {
            if (showDefaultSelection && defaultSelection != null)
                defaultSelection.Select();
            else
                HideDetails();
        }

        public void ShowDetails(Sprite icon, string moduleName, string description)
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

        public void HideDetails()
        {
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
    }
}
