using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dungeon.UI
{
    using Dungeon.System;

    /// <summary>
    /// 공통 하단 내비게이션 버튼의 현재 씬, 포인터 호버, 선택 상태를 표시합니다.
    /// 씬 이동 자체는 기존 LoadSceneByNameButton에 위임합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button), typeof(LoadSceneByNameButton))]
    public sealed class BottomNavigationButtonUI : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler
    {
        [Header("Navigation")]
        [SerializeField] private LoadSceneByNameButton navigation;
        [SerializeField] private Button button;

        [Header("Visuals")]
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image highlight;
        [SerializeField] private RectTransform contentRoot;

        [Header("Colors")]
        [SerializeField] private Color inactiveColor = new(0.553f, 0.553f, 0.553f, 1f);
        [SerializeField] private Color focusedColor = Color.white;
        [SerializeField, Range(0.8f, 1f)] private float pressedScale = 0.97f;

        private bool isCurrentScene;
        private bool pointerInside;
        private bool selected;
        private bool pressed;
        private Vector3 contentBaseScale = Vector3.one;

        public string TargetSceneName => navigation != null ? navigation.SceneName : string.Empty;
        public bool IsCurrentScene => isCurrentScene;

        private void Awake()
        {
            ResolveReferences();
            CaptureBaseScale();
            RefreshSceneState();
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            RefreshSceneState();
        }

        private void Start()
        {
            RefreshSceneState();
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            pointerInside = false;
            selected = false;
            pressed = false;
            RefreshVisual();
        }

        public void Configure(
            LoadSceneByNameButton sceneNavigation,
            Button targetButton,
            Image targetIcon,
            TMP_Text targetLabel,
            Image targetHighlight,
            RectTransform targetContent)
        {
            navigation = sceneNavigation;
            button = targetButton;
            icon = targetIcon;
            label = targetLabel;
            highlight = targetHighlight;
            contentRoot = targetContent;
            CaptureBaseScale();
            RefreshSceneState();
        }

        public void RefreshSceneState()
        {
            ApplySceneState(SceneManager.GetActiveScene().name);
        }

        /// <summary>
        /// 에디터 빌더와 런타임이 같은 규칙으로 현재 씬 표시를 적용할 때 사용합니다.
        /// </summary>
        public void ApplySceneState(string currentSceneName)
        {
            ResolveReferences();
            isCurrentScene = navigation != null && navigation.IsTargetScene(currentSceneName);

            if (navigation != null)
                navigation.RefreshStateForScene(currentSceneName);

            pointerInside = false;
            selected = false;
            pressed = false;
            RefreshVisual();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isCurrentScene)
                return;

            pointerInside = true;
            RefreshVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            pressed = false;
            RefreshVisual();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (isCurrentScene || eventData.button != PointerEventData.InputButton.Left)
                return;

            pressed = true;
            RefreshVisual();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
            RefreshVisual();
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (isCurrentScene)
                return;

            selected = true;
            RefreshVisual();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            pressed = false;
            RefreshVisual();
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            ApplySceneState(nextScene.name);
        }

        private void ResolveReferences()
        {
            if (navigation == null)
                navigation = GetComponent<LoadSceneByNameButton>();
            if (button == null)
                button = GetComponent<Button>();
        }

        private void CaptureBaseScale()
        {
            contentBaseScale = contentRoot != null ? contentRoot.localScale : Vector3.one;
        }

        private void RefreshVisual()
        {
            bool focused = isCurrentScene || pointerInside || selected;
            Color color = focused ? focusedColor : inactiveColor;

            if (icon != null)
                icon.color = color;
            if (label != null)
                label.color = color;
            if (highlight != null)
                highlight.gameObject.SetActive(!isCurrentScene && (pointerInside || selected));
            if (contentRoot != null)
                contentRoot.localScale = contentBaseScale * (pressed ? pressedScale : 1f);
        }
    }
}
