using UnityEngine;

namespace Dungeon.UI
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class DungeonInfoPopupLayout : MonoBehaviour
    {
        [SerializeField, Range(.1f, 1f)] private float sizeMultiplier = .8f;
        [SerializeField, Min(0)] private float pointGap = 24f;
        private RectTransform point;
        private readonly Vector3[] corners = new Vector3[4];
        private bool applying;
        public void SetAnchor(RectTransform anchor) { point = anchor; RefreshLayout(); }
        private void OnEnable() => RefreshLayout();
        private void OnRectTransformDimensionsChange() => RefreshLayout();
        private void LateUpdate() => RefreshLayout();
        public void RefreshLayout()
        {
            if (!isActiveAndEnabled || applying) return;
            var canvas = GetComponentInParent<Canvas>(); if (canvas == null) return;
            var area = (RectTransform)canvas.rootCanvas.transform;
            Rect content = StatusWindowLayout.GetContentArea(canvas.rootCanvas);
            if (content.width <= 0 || content.height <= 0) return;
            applying = true;
            try
            {
                var rect = (RectTransform)transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
                Vector2 referenceSize = new Vector2(776, 484);
                rect.sizeDelta = referenceSize;
                float responsive = Mathf.Min(1, content.width / 1920, content.height / 949);
                float scale = sizeMultiplier * responsive;
                Vector2 center = content.center;
                if (point != null && point.gameObject.activeInHierarchy)
                {
                    point.GetWorldCorners(corners);
                    Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
                    foreach (Vector3 corner in corners)
                    {
                        Vector2 local = area.InverseTransformPoint(corner);
                        min = Vector2.Min(min, local); max = Vector2.Max(max, local);
                    }
                    Vector2 anchor = (min + max) * .5f, pointHalf = (max - min) * .5f;
                    float gap = pointGap * responsive, margin = 12f * responsive;
                    // 기존 45도 배치의 가로 간격/크기를 유지하고, 세로 상승량만 30도로 낮춥니다.
                    float right = content.xMax - margin - anchor.x;
                    float top = content.yMax - margin - anchor.y;
                    float clearance = pointHalf.y + gap;
                    float fit = Mathf.Min((top - clearance) / referenceSize.y,
                        (right - clearance) / ((referenceSize.x + referenceSize.y) * .5f));
                    scale = Mathf.Max(.01f, Mathf.Min(scale, fit));
                    Vector2 half = referenceSize * scale * .5f;
                    float distance = Mathf.Max(half.x + pointHalf.x, half.y + pointHalf.y) + gap;
                    distance = Mathf.Min(distance, right - half.x, top - half.y);
                    center = anchor + new Vector2(distance, distance * Mathf.Tan(30f * Mathf.Deg2Rad));
                }
                rect.localScale = new Vector3(scale, scale, 1);
                rect.position = area.TransformPoint(center);
            }
            finally { applying = false; }
        }
    }
}
