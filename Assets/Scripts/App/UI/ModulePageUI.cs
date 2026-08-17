using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dungeon.UI
{
    /// <summary>
    /// 모듈 페이지의 선택 상태와 상세 정보 표시를 관리합니다.
    /// 저장 데이터나 장착 규칙은 다루지 않으며, 슬롯 선택 결과만 화면에 반영합니다.
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

        private ModuleSlotUI selectedSlot;

        public ModuleSlotUI SelectedSlot => selectedSlot;

        private void Start()
        {
            if (showDefaultSelection && defaultSelection != null)
                defaultSelection.Select();
            else
                HideDetails();
        }

        /// <summary>
        /// 슬롯에서 전달된 모듈을 현재 선택으로 만들고 상세 정보를 갱신합니다.
        /// </summary>
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

                if (selectedSlot != null)
                    selectedSlot.SetSelected(true);
            }

            ApplyDetails(icon, moduleName, description);
        }

        /// <summary>
        /// 이전 씬/프리팹과의 호환을 위한 상세 표시 API입니다.
        /// </summary>
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
    }
}
