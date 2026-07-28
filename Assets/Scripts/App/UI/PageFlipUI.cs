using UnityEngine;
using UnityEngine.UI;

namespace Dungeon.UI
{
    /// <summary>
    /// 하나의 버튼을 누를 때마다 Main Page와 Sub Page를 전환합니다.
    ///
    /// 아이템 페이지뿐 아니라 다른 상태창 페이지에서도
    /// 공통으로 사용할 수 있습니다.
    /// </summary>
    public class PageFlipUI : MonoBehaviour
    {
        [Header("Button")]
        [Tooltip("페이지 전환에 사용할 버튼입니다. 비워두면 이 오브젝트의 Button을 찾습니다.")]
        [SerializeField] private Button flipButton;

        [Header("Pages")]
        [Tooltip("처음 표시할 기본 페이지입니다.")]
        [SerializeField] private GameObject mainPage;

        [Tooltip("버튼을 눌렀을 때 전환할 보조 페이지입니다.")]
        [SerializeField] private GameObject subPage;

        [Header("Initial State")]
        [Tooltip("시작할 때 Main Page를 표시합니다. 체크를 해제하면 Sub Page부터 표시합니다.")]
        [SerializeField] private bool startWithMainPage = true;

        [Tooltip("이 오브젝트가 다시 활성화될 때마다 시작 페이지로 되돌립니다.")]
        [SerializeField] private bool resetToStartPageOnEnable = false;

        [Header("Debug")]
        [SerializeField] private bool logPageChange = false;

        private bool showingMainPage;
        private bool initialized;

        public bool IsShowingMainPage => showingMainPage;

        private void Awake()
        {
            AutoBindButton();
            BindButton();

            showingMainPage = startWithMainPage;
            ApplyPageState();

            initialized = true;
        }

        private void OnEnable()
        {
            // Awake 이전에 OnEnable이 호출되는 경우를 방지합니다.
            if (!initialized)
                return;

            if (resetToStartPageOnEnable)
            {
                showingMainPage = startWithMainPage;
                ApplyPageState();
            }
        }

        private void OnDestroy()
        {
            if (flipButton != null)
                flipButton.onClick.RemoveListener(FlipPage);
        }

        /// <summary>
        /// 현재 페이지를 반대 페이지로 전환합니다.
        /// Button 클릭 시 호출됩니다.
        /// </summary>
        public void FlipPage()
        {
            if (!ValidatePages())
                return;

            showingMainPage = !showingMainPage;
            ApplyPageState();
        }

        /// <summary>
        /// Main Page를 직접 표시합니다.
        /// </summary>
        public void ShowMainPage()
        {
            showingMainPage = true;
            ApplyPageState();
        }

        /// <summary>
        /// Sub Page를 직접 표시합니다.
        /// </summary>
        public void ShowSubPage()
        {
            showingMainPage = false;
            ApplyPageState();
        }

        /// <summary>
        /// 외부 코드에서 페이지 상태를 직접 지정할 때 사용합니다.
        /// true면 Main Page, false면 Sub Page를 표시합니다.
        /// </summary>
        public void SetMainPageVisible(bool visible)
        {
            showingMainPage = visible;
            ApplyPageState();
        }

        private void ApplyPageState()
        {
            if (!ValidatePages())
                return;

            mainPage.SetActive(showingMainPage);
            subPage.SetActive(!showingMainPage);

            if (logPageChange)
            {
                Debug.Log(
                    $"[PageFlipUI] Page changed. " +
                    $"MainPage={showingMainPage}, " +
                    $"ActivePage={(showingMainPage ? mainPage.name : subPage.name)}",
                    this
                );
            }
        }

        private void AutoBindButton()
        {
            if (flipButton == null)
                flipButton = GetComponent<Button>();
        }

        private void BindButton()
        {
            if (flipButton == null)
            {
                Debug.LogWarning(
                    "[PageFlipUI] Flip Button이 연결되지 않았습니다.",
                    this
                );

                return;
            }

            // 중복 등록 방지
            flipButton.onClick.RemoveListener(FlipPage);
            flipButton.onClick.AddListener(FlipPage);
        }

        private bool ValidatePages()
        {
            if (mainPage == null || subPage == null)
            {
                Debug.LogWarning(
                    "[PageFlipUI] Main Page 또는 Sub Page가 연결되지 않았습니다.",
                    this
                );

                return false;
            }

            if (mainPage == subPage)
            {
                Debug.LogWarning(
                    "[PageFlipUI] Main Page와 Sub Page에 같은 오브젝트가 연결되어 있습니다.",
                    this
                );

                return false;
            }

            return true;
        }
    }
}