using UnityEngine;

namespace Dungeon.UI.Tutorial
{
    /// <summary>상태창과 같은 기준 크기의 슬라이드와 바깥쪽 조작 버튼을 배치합니다.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class TutorialWindowLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform slide, previous, next, close;
        [SerializeField] private RectTransform slideBackground;
        private bool applying;
        public void Configure(RectTransform image, RectTransform back, RectTransform forward, RectTransform dismiss, RectTransform background = null)
        { slide = image; previous = back; next = forward; close = dismiss; slideBackground = background; RefreshLayout(); }
        private void OnEnable() => RefreshLayout();
        private void OnRectTransformDimensionsChange() => RefreshLayout();
        private void LateUpdate() => RefreshLayout();

        public void RefreshLayout()
        {
            if (!isActiveAndEnabled || applying || slide == null || previous == null || next == null || close == null) return;
            var body = (RectTransform)transform;
            Rect area = body.rect;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null || area.width <= 0 || area.height <= 0) return;
            var root = (RectTransform)canvas.rootCanvas.transform;
            Rect window = StatusWindowLayout.GetWindowRect(canvas.rootCanvas);
            if (window.width <= 0 || window.height <= 0) return;
            applying = true;
            try
            {
                // 상태창과 같은 계산을 사용해 위치와 크기가 정확히 겹치게 합니다.
                Vector2 center = body.InverseTransformPoint(root.TransformPoint(window.center));
                Vector2 size = new(window.width * root.lossyScale.x / body.lossyScale.x,
                    window.height * root.lossyScale.y / body.lossyScale.y);
                Place(slide, center - area.center, size);
                if (slideBackground != null) Place(slideBackground, center - area.center, size);
                float margin = Mathf.Max(0, (area.width - size.x) * .5f);
                float inset = Mathf.Min(72, margin * .5f);
                float arrowScale = Mathf.Clamp(margin / 100, .5f, 1);
                Place(previous, new Vector2(area.xMin + inset, center.y) - area.center, new Vector2(168, 231) * arrowScale);
                Place(next, new Vector2(area.xMax - inset, center.y) - area.center, new Vector2(168, 231) * arrowScale);
                float slideBottom = center.y - size.y * .5f;
                float below = Mathf.Max(0, slideBottom - area.yMin);
                float closeScale = Mathf.Clamp((below - 16) / 82, .6f, 1);
                // PNGの灰色部分は中央82px。下側の余白が狭い場合もボタンを画面内に残します。
                float halfVisible = 41 * closeScale;
                float closeY = Mathf.Max(slideBottom - 24 - halfVisible, area.yMin + halfVisible + 8);
                Place(close, new Vector2(center.x, closeY) - area.center, new Vector2(308, 166) * closeScale);
            }
            finally { applying = false; }
        }
        private static void Place(RectTransform rect, Vector2 center, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.localScale = Vector3.one;
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
        }
    }
}
