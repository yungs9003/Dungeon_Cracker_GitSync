using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dungeon.UI.Tutorial
{
    [RequireComponent(typeof(Button), typeof(Image))]
    public sealed class TutorialLauncherUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private TutorialSlides tutorial;
        [SerializeField] private TutorialOverlayUI overlayPrefab;
        [SerializeField] private Sprite normalSprite, hoverSprite;
        private bool hovered, pressed;
        private Vector2 originalPosition;
        public TutorialSlides Tutorial => tutorial;
        public void Configure(TutorialSlides data, TutorialOverlayUI prefab, Sprite normal, Sprite hover)
        { tutorial = data; overlayPrefab = prefab; normalSprite = normal; hoverSprite = hover; Refresh(); }
        private void OnEnable()
        { originalPosition = ((RectTransform)transform).anchoredPosition; hovered = pressed = false; GetComponent<Button>().onClick.AddListener(Open); Refresh(); }
        private void OnDisable()
        {
            GetComponent<Button>().onClick.RemoveListener(Open); hovered = pressed = false; Refresh();
            ((RectTransform)transform).anchoredPosition = originalPosition;
            if (TutorialOverlayUI.Active != null && TutorialOverlayUI.Active.Owner == this) TutorialOverlayUI.Active.Close();
        }
        public void Open() => TutorialOverlayUI.Show(overlayPrefab, tutorial, this);
        public void OnPointerEnter(PointerEventData data) { hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData data) { hovered = false; Refresh(); }
        public void OnPointerDown(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) { pressed = true; Refresh(); } }
        public void OnPointerUp(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) { pressed = false; Refresh(); } }
        private void LateUpdate()
        {
            // 기존 고정 크기 패널이 작은 화면 밖으로 나가도 도움말 버튼은 접근할 수 있게 합니다.
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var area = (RectTransform)canvas.rootCanvas.transform;
            var rect = (RectTransform)transform; rect.anchoredPosition = originalPosition;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(area, rect);
            Vector2 shift = Vector2.zero;
            if (bounds.min.x < area.rect.xMin + 8) shift.x = area.rect.xMin + 8 - bounds.min.x;
            else if (bounds.max.x > area.rect.xMax - 8) shift.x = area.rect.xMax - 8 - bounds.max.x;
            if (bounds.min.y < area.rect.yMin + 8) shift.y = area.rect.yMin + 8 - bounds.min.y;
            else if (bounds.max.y > area.rect.yMax - 8) shift.y = area.rect.yMax - 8 - bounds.max.y;
            rect.position += area.TransformVector(shift);
        }
        private void Refresh() { var image = GetComponent<Image>(); image.sprite = hovered || pressed ? hoverSprite : normalSprite; }
    }
}
