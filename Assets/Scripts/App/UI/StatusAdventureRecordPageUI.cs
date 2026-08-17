using UnityEngine;
using UnityEngine.UI;

namespace Dungeon.UI
{
    /// <summary>
    /// Status 탭의 기본 화면과 Adventure Record 오버레이를 전환합니다.
    /// 전환 버튼은 StatusClipMask 직속 오버레이이므로 두 화면 모두에서 유지됩니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StatusAdventureRecordPageUI : MonoBehaviour
    {
        [Header("Tab")]
        [SerializeField] private StatusTabSwitcher tabSwitcher;
        [SerializeField] private GameObject statusTabContent;
        [SerializeField] private int statusTabIndex;

        [Header("Page Flip")]
        [SerializeField] private Button flipButton;
        [SerializeField] private GameObject adventureRecordPage;
        [SerializeField] private bool resetToMainPageOnEnable = true;

        [Header("Other Overlay Controls")]
        [Tooltip("Item 페이지의 기존 PageFlipUI 버튼입니다. 전역 오버레이로 옮긴 뒤 Item 탭에서만 표시합니다.")]
        [SerializeField] private GameObject itemFlipButton;
        [SerializeField] private int itemTabIndex = 3;

        private bool showingAdventureRecord;
        private bool buttonBound;

        public bool IsShowingAdventureRecord => showingAdventureRecord;
        public Button FlipButton => flipButton;
        public GameObject AdventureRecordPage => adventureRecordPage;

        private void Awake()
        {
            ResolveReferences();
            BindButton();
            showingAdventureRecord = false;
            ApplyState();
        }

        private void OnEnable()
        {
            ResolveReferences();
            BindButton();

            if (tabSwitcher != null)
                tabSwitcher.TabSelected += HandleTabSelected;

            if (resetToMainPageOnEnable)
                showingAdventureRecord = false;

            ApplyState();
        }

        private void OnDisable()
        {
            if (tabSwitcher != null)
                tabSwitcher.TabSelected -= HandleTabSelected;

            if (resetToMainPageOnEnable)
            {
                showingAdventureRecord = false;

                if (adventureRecordPage != null)
                    adventureRecordPage.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (buttonBound && flipButton != null)
                flipButton.onClick.RemoveListener(FlipPage);
        }

        public void Configure(
            StatusTabSwitcher targetTabSwitcher,
            GameObject targetStatusTabContent,
            Button targetFlipButton,
            GameObject targetAdventureRecordPage,
            GameObject targetItemFlipButton,
            int targetStatusTabIndex = 0,
            int targetItemTabIndex = 3,
            bool resetOnEnable = true)
        {
            if (buttonBound && flipButton != null)
                flipButton.onClick.RemoveListener(FlipPage);

            buttonBound = false;
            tabSwitcher = targetTabSwitcher;
            statusTabContent = targetStatusTabContent;
            flipButton = targetFlipButton;
            adventureRecordPage = targetAdventureRecordPage;
            itemFlipButton = targetItemFlipButton;
            statusTabIndex = targetStatusTabIndex;
            itemTabIndex = targetItemTabIndex;
            resetToMainPageOnEnable = resetOnEnable;
            showingAdventureRecord = false;
            ApplyState();
        }

        public void FlipPage()
        {
            if (!IsStatusTabSelected() || adventureRecordPage == null)
                return;

            showingAdventureRecord = !showingAdventureRecord;
            ApplyState();
        }

        public void ShowMainPage()
        {
            showingAdventureRecord = false;
            ApplyState();
        }

        public void ShowAdventureRecord()
        {
            if (!IsStatusTabSelected())
                return;

            showingAdventureRecord = true;
            ApplyState();
        }

        private void HandleTabSelected(int selectedIndex)
        {
            if (selectedIndex != statusTabIndex)
                showingAdventureRecord = false;

            ApplyState();
        }

        private void ResolveReferences()
        {
            if (flipButton == null)
            {
                Transform buttonTransform = transform.Find("BottomRightArrowButton");

                if (buttonTransform == null)
                {
                    buttonTransform = transform.Find(
                        "OverlayControls/StatusPage/BottomRightArrowButton"
                    );
                }

                if (buttonTransform != null)
                    flipButton = buttonTransform.GetComponent<Button>();
            }
        }

        private void BindButton()
        {
            if (buttonBound || flipButton == null)
                return;

            flipButton.onClick.RemoveListener(FlipPage);
            flipButton.onClick.AddListener(FlipPage);
            buttonBound = true;
        }

        private bool IsStatusTabSelected()
        {
            if (tabSwitcher != null && tabSwitcher.CurrentIndex >= 0)
                return tabSwitcher.CurrentIndex == statusTabIndex;

            return statusTabContent == null || statusTabContent.activeSelf;
        }

        private void ApplyState()
        {
            bool statusSelected = IsStatusTabSelected();

            if (!statusSelected)
                showingAdventureRecord = false;

            if (adventureRecordPage != null)
                adventureRecordPage.SetActive(statusSelected && showingAdventureRecord);

            if (flipButton != null)
                flipButton.gameObject.SetActive(statusSelected);

            if (itemFlipButton != null)
                itemFlipButton.SetActive(IsTabSelected(itemTabIndex));
        }

        private bool IsTabSelected(int tabIndex)
        {
            return tabSwitcher != null && tabSwitcher.CurrentIndex == tabIndex;
        }
    }
}
