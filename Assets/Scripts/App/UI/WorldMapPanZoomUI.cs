using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dungeon.UI
{
    /// <summary>원본 픽셀 크기의 지도와 자식 지점들을 함께 이동하고, 선택 지점을 중앙으로 확대합니다.</summary>
    public sealed class WorldMapPanZoomUI : MonoBehaviour,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private Image mapImage;
        [SerializeField] private RectTransform bottomBar;
        [SerializeField, Min(.01f)] private float focusDuration = .5f;
        [SerializeField, Min(1f)] private float maxZoom = 3f;
        [Tooltip("관성 감속 계수입니다. 클수록 빨리 멈춥니다.")]
        [SerializeField, Min(.01f)] private float inertiaDamping = 8f;
        [SerializeField, Min(0f)] private float maxInertiaSpeed = 1500f;
        private bool dragging, animating;
        private int dragPointer, suppressClickThroughFrame = -1;
        private Vector2 previousPointer, fromPosition, toPosition;
        private float fromZoom, toZoom, elapsed;
        private Action completed;
        private Vector2 velocity, returnPosition;
        private float lastDragEventTime;
        private bool returnPending;
        private readonly Vector3[] barCorners = new Vector3[4];

        public RectTransform Content => content;
        public RectTransform Viewport => viewport;
        public float Zoom => content != null ? content.localScale.x : 1f;
        public bool IsAnimating => animating;
        public bool CanClickPoint => !dragging && Time.frameCount > suppressClickThroughFrame;

        private void Awake() { ApplyNativeSize(); ApplyPose(Vector2.zero, 1f); RefreshViewport(); }
        private void LateUpdate() => RefreshViewport();
        private void OnDisable() { CancelFocus(); StopInertia(); dragging = false; returnPending = false; }

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
            if (viewport == null || content == null) return;
            if (bottomBar != null && viewport.parent is RectTransform parent)
            {
                bottomBar.GetWorldCorners(barCorners);
                float top = float.NegativeInfinity;
                foreach (Vector3 corner in barCorners) top = Mathf.Max(top, parent.InverseTransformPoint(corner).y);
                float inset = Mathf.Clamp(top - parent.rect.yMin, 0f, parent.rect.height);
                if (Mathf.Abs(viewport.offsetMin.y - inset) > .01f)
                    viewport.offsetMin = new Vector2(viewport.offsetMin.x, inset);
            }
            content.anchoredPosition = ClampPosition(content.anchoredPosition, Zoom);
        }

        public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = true;
        public void OnPointerDown(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) StopInertia(); }
        public void StopInertia() => velocity = Vector2.zero;
        public void OnBeginDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || dragging || content == null) return;
            if (!RectTransformUtility.RectangleContainsScreenPoint(content, data.position, data.pressEventCamera)) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, data.position, data.pressEventCamera, out previousPointer)) return;
            CancelFocus(); StopInertia(); returnPending = false;
            dragging = true; dragPointer = data.pointerId; lastDragEventTime = Time.unscaledTime;
        }
        public void OnDrag(PointerEventData data)
        {
            if (!dragging || data.pointerId != dragPointer) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, data.position, data.pressEventCamera, out Vector2 point)) return;
            Vector2 before = content.anchoredPosition;
            Vector2 desired = before + point - previousPointer;
            content.anchoredPosition = ClampPosition(desired, Zoom);
            float dt = Mathf.Max(.001f, Time.unscaledDeltaTime);
            Vector2 sampled = Vector2.ClampMagnitude((content.anchoredPosition - before) / dt, maxInertiaSpeed);
            velocity = Vector2.Lerp(velocity, sampled, 1f - Mathf.Exp(-12f * dt));
            if (Mathf.Abs(content.anchoredPosition.x - desired.x) > .001f) velocity.x = 0f;
            if (Mathf.Abs(content.anchoredPosition.y - desired.y) > .001f) velocity.y = 0f;
            lastDragEventTime = Time.unscaledTime;
            previousPointer = point;
        }
        public void OnEndDrag(PointerEventData data)
        {
            if (!dragging || data.pointerId != dragPointer) return;
            dragging = false;
            if (Time.unscaledTime - lastDragEventTime > .08f) StopInertia();
            // 지점 버튼에서 시작한 드래그를 클릭으로 오인하지 않습니다.
            suppressClickThroughFrame = Time.frameCount + 1;
        }

        public void FocusOn(RectTransform point, float zoom, Action onCompleted = null)
        {
            if (point == null || content == null || !point.IsChildOf(content)) return;
            if (!returnPending)
            {
                returnPosition = ClampPosition(content.anchoredPosition / Mathf.Max(.001f, Zoom), 1f);
                returnPending = true;
            }
            Vector3 local = content.InverseTransformPoint(point.TransformPoint(point.rect.center));
            float targetZoom = Mathf.Clamp(zoom, 1f, Mathf.Max(1f, maxZoom));
            AnimateTo(-(Vector2)local * targetZoom, targetZoom, onCompleted);
        }

        public void ResetView() { returnPending = false; AnimateTo(Vector2.zero, 1f, null); }

        public void ReturnFromFocus()
        {
            StopInertia();
            if (!returnPending) return;
            returnPending = false;
            AnimateTo(returnPosition, 1f, null);
        }

        private void AnimateTo(Vector2 position, float zoom, Action onCompleted)
        {
            if (content == null) return;
            CancelFocus(); StopInertia(); dragging = false;
            fromPosition = content.anchoredPosition; fromZoom = Zoom;
            toPosition = ClampPosition(position, zoom); toZoom = zoom;
            completed = onCompleted; elapsed = 0f; animating = true;
        }
        private void Update()
        {
            if (!animating)
            {
                if (dragging) { if (Time.unscaledTime - lastDragEventTime > .08f) StopInertia(); }
                else TickInertia(Time.unscaledDeltaTime);
                return;
            }
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(.01f, focusDuration));
            float ease = t * t * (3f - 2f * t);
            ApplyPose(Vector2.LerpUnclamped(fromPosition, toPosition, ease), Mathf.LerpUnclamped(fromZoom, toZoom, ease));
            if (t < 1f) return;
            animating = false;
            Action callback = completed; completed = null;
            callback?.Invoke();
        }
        private void TickInertia(float dt)
        {
            if (content == null || velocity.sqrMagnitude < 64f) { StopInertia(); return; }
            float damping = Mathf.Max(.01f, inertiaDamping);
            float decay = Mathf.Exp(-damping * dt);
            Vector2 desired = content.anchoredPosition + velocity * ((1f - decay) / damping);
            content.anchoredPosition = ClampPosition(desired, Zoom);
            velocity *= decay;
            if (Mathf.Abs(content.anchoredPosition.x - desired.x) > .001f) velocity.x = 0f;
            if (Mathf.Abs(content.anchoredPosition.y - desired.y) > .001f) velocity.y = 0f;
        }
        private void CancelFocus() { animating = false; completed = null; }
        private void ApplyPose(Vector2 position, float zoom)
        {
            if (content == null) return;
            content.localScale = new Vector3(zoom, zoom, 1f); content.anchoredPosition = ClampPosition(position, zoom);
        }
        private Vector2 ClampPosition(Vector2 position, float zoom)
        {
            // 이미지의 끝을 넘겨 빈 배경이 드러나지 않도록 합니다. 화면보다 작은 축은 중앙에 고정합니다.
            Vector2 limit = Vector2.Max(Vector2.zero, (content.rect.size * zoom - viewport.rect.size) * .5f);
            return new Vector2(Mathf.Clamp(position.x, -limit.x, limit.x), Mathf.Clamp(position.y, -limit.y, limit.y));
        }
    }
}
