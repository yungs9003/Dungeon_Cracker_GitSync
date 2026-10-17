using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dungeon.UI
{
    /// <summary>원본 픽셀 크기의 지도와 자식 지점들을 함께 이동하고, 선택 지점을 중앙으로 확대합니다.</summary>
    public sealed class WorldMapPanZoomUI : MonoBehaviour,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private Image mapImage;
        [SerializeField] private RectTransform bottomBar;
        [SerializeField, Min(.01f)] private float focusDuration = .5f;
        [SerializeField, Min(1f)] private float maxZoom = 3f;
        private bool dragging, animating;
        private int dragPointer, suppressClickThroughFrame = -1;
        private Vector2 previousPointer, fromPosition, toPosition;
        private float fromZoom, toZoom, elapsed;
        private Action completed;
        private readonly Vector3[] barCorners = new Vector3[4];

        public RectTransform Content => content;
        public RectTransform Viewport => viewport;
        public float Zoom => content != null ? content.localScale.x : 1f;
        public bool IsAnimating => animating;
        public bool CanClickPoint => !dragging && Time.frameCount > suppressClickThroughFrame;

        private void Awake() { ApplyNativeSize(); ApplyPose(Vector2.zero, 1f); RefreshViewport(); }
        private void LateUpdate() => RefreshViewport();
        private void OnDisable() { CancelFocus(); dragging = false; }

        public void Configure(RectTransform window, RectTransform map, Image image, RectTransform navigationBar)
        {
            viewport = window; content = map; mapImage = image; bottomBar = navigationBar;
            ApplyNativeSize(); ApplyPose(Vector2.zero, 1f); RefreshViewport();
        }

        public void ApplyNativeSize()
        {
            if (content == null || mapImage == null || mapImage.sprite == null) return;
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f);
            content.sizeDelta = mapImage.sprite.rect.size;
        }

        public void RefreshViewport()
        {
            if (viewport == null || bottomBar == null || !(viewport.parent is RectTransform parent)) return;
            bottomBar.GetWorldCorners(barCorners);
            float top = float.NegativeInfinity;
            foreach (Vector3 corner in barCorners) top = Mathf.Max(top, parent.InverseTransformPoint(corner).y);
            float inset = Mathf.Clamp(top - parent.rect.yMin, 0f, parent.rect.height);
            if (Mathf.Abs(viewport.offsetMin.y - inset) > .01f)
                viewport.offsetMin = new Vector2(viewport.offsetMin.x, inset);
        }

        public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = true;
        public void OnBeginDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || dragging || content == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, data.position, data.pressEventCamera, out previousPointer)) return;
            CancelFocus(); dragging = true; dragPointer = data.pointerId;
        }
        public void OnDrag(PointerEventData data)
        {
            if (!dragging || data.pointerId != dragPointer) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, data.position, data.pressEventCamera, out Vector2 point)) return;
            content.anchoredPosition = ClampPosition(content.anchoredPosition + point - previousPointer, Zoom);
            previousPointer = point;
        }
        public void OnEndDrag(PointerEventData data)
        {
            if (!dragging || data.pointerId != dragPointer) return;
            dragging = false;
            // 지점 버튼에서 시작한 드래그를 클릭으로 오인하지 않습니다.
            suppressClickThroughFrame = Time.frameCount + 1;
        }

        public void FocusOn(RectTransform point, float zoom, Action onCompleted = null)
        {
            if (point == null || content == null || !point.IsChildOf(content)) return;
            Vector3 local = content.InverseTransformPoint(point.TransformPoint(point.rect.center));
            float targetZoom = Mathf.Clamp(zoom, 1f, Mathf.Max(1f, maxZoom));
            AnimateTo(-(Vector2)local * targetZoom, targetZoom, onCompleted);
        }

        public void ResetView() => AnimateTo(Vector2.zero, 1f, null);

        private void AnimateTo(Vector2 position, float zoom, Action onCompleted)
        {
            if (content == null) return;
            CancelFocus(); dragging = false;
            fromPosition = content.anchoredPosition; fromZoom = Zoom;
            toPosition = ClampPosition(position, zoom); toZoom = zoom;
            completed = onCompleted; elapsed = 0f; animating = true;
        }
        private void Update()
        {
            if (!animating) return;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(.01f, focusDuration));
            float ease = t * t * (3f - 2f * t);
            ApplyPose(Vector2.LerpUnclamped(fromPosition, toPosition, ease), Mathf.LerpUnclamped(fromZoom, toZoom, ease));
            if (t < 1f) return;
            animating = false;
            Action callback = completed; completed = null;
            callback?.Invoke();
        }
        private void CancelFocus() { animating = false; completed = null; }
        private void ApplyPose(Vector2 position, float zoom)
        {
            if (content == null) return;
            content.anchoredPosition = position; content.localScale = new Vector3(zoom, zoom, 1f);
        }
        private Vector2 ClampPosition(Vector2 position, float zoom)
        {
            // 지도 가장자리의 지점도 중앙에 올 수 있도록 여백을 허용하되, 지도가 완전히 화면 밖으로 나가지는 않게 합니다.
            Vector2 half = content.rect.size * (zoom * .5f);
            return new Vector2(Mathf.Clamp(position.x, -half.x, half.x), Mathf.Clamp(position.y, -half.y, half.y));
        }
    }
}
