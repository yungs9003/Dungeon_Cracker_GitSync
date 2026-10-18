using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dungeon.UI.Tutorial
{
    /// <summary>각 패널의 슬라이드 목록을 전체 화면의 공통 모달로 표시합니다.</summary>
    public sealed class TutorialOverlayUI : MonoBehaviour
    {
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private Image slideImage;
        [SerializeField] private Button previousButton, nextButton, closeButton;
        private readonly List<Sprite> slides = new();
        private GameObject previousSelection;
        public static TutorialOverlayUI Active { get; private set; }
        public static bool IsAnyOpen => Active != null && Active.IsOpen;
        public TutorialLauncherUI Owner { get; private set; }
        public bool IsOpen => modalRoot != null && modalRoot.activeSelf;
        public int CurrentIndex { get; private set; }
        public int SlideCount => slides.Count;

        public void Configure(GameObject root, Image image, Button previous, Button next, Button close)
        { modalRoot = root; slideImage = image; previousButton = previous; nextButton = next; closeButton = close; modalRoot.SetActive(false); }
        private void Awake()
        {
            previousButton.onClick.AddListener(Previous);
            nextButton.onClick.AddListener(Next);
            closeButton.onClick.AddListener(Close);
            modalRoot.SetActive(false);
        }
        public static TutorialOverlayUI Show(TutorialOverlayUI prefab, TutorialSlides data, TutorialLauncherUI owner)
        {
            if (prefab == null || data == null) return null;
            if (Active == null)
            {
                Active = Instantiate(prefab);
                Active.name = "TutorialOverlay";
                if (owner != null && owner.gameObject.scene.IsValid()) SceneManager.MoveGameObjectToScene(Active.gameObject, owner.gameObject.scene);
            }
            return Active.Open(data, owner) ? Active : null;
        }
        private bool Open(TutorialSlides data, TutorialLauncherUI owner)
        {
            if (IsOpen) Close();
            slides.Clear();
            if (data.slides != null) foreach (var sprite in data.slides) if (sprite != null) slides.Add(sprite);
            if (slides.Count == 0) return false;
            Owner = owner; CurrentIndex = 0;
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            modalRoot.SetActive(true); Refresh();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
            return true;
        }
        public void Previous() { if (!IsOpen) return; CurrentIndex = Mathf.Max(0, CurrentIndex - 1); Refresh(); }
        public void Next() { if (!IsOpen) return; CurrentIndex = Mathf.Min(slides.Count - 1, CurrentIndex + 1); Refresh(); }
        private void Refresh()
        {
            slideImage.sprite = slides[CurrentIndex];
            slideImage.preserveAspect = true;
            previousButton.interactable = CurrentIndex > 0;
            nextButton.interactable = CurrentIndex < slides.Count - 1;
        }
        public void Close()
        {
            bool wasOpen = IsOpen;
            if (modalRoot != null) modalRoot.SetActive(false);
            Owner = null;
            if (wasOpen && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
            previousSelection = null;
        }
        private void OnDisable() => Close();
        private void OnDestroy() { if (Active == this) Active = null; }
    }
}
