using UnityEngine;

namespace Dungeon.UI
{
    /// <summary>하단 내비게이션을 제외한 화면 중앙에 상태창을 배치합니다.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class StatusWindowLayout : MonoBehaviour
    {
        public static readonly Vector2 ReferenceSize = new(1584, 888);
        public const float ReferenceScreenWidth = 1920;
        private bool applying;
        private void OnEnable() => RefreshLayout();
        private void OnRectTransformDimensionsChange() => RefreshLayout();
        private void LateUpdate() => RefreshLayout();

        public void RefreshLayout()
        {
            if (applying) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var area = (RectTransform)canvas.rootCanvas.transform;
            Rect window = GetWindowRect(canvas.rootCanvas);
            if (window.width <= 0 || window.height <= 0) return;
            applying = true;
            try
            {
                var rect = (RectTransform)transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
                rect.sizeDelta = ReferenceSize;
                float scale = window.width / ReferenceSize.x;
                rect.localScale = new Vector3(scale, scale, 1);
                rect.position = area.TransformPoint(window.center);
            }
            finally { applying = false; }
        }

        /// <summary>상태창과 튜토리얼이 동일하게 사용하는 화면 영역입니다.</summary>
        public static Rect GetWindowRect(Canvas canvas)
        {
            Rect available = GetContentArea(canvas);
            if (available.width <= 0 || available.height <= 0) return new Rect();
            float scale = Mathf.Min(1, available.width / ReferenceScreenWidth, available.height / ReferenceSize.y);
            Vector2 size = ReferenceSize * scale;
            return new Rect(available.center - size * .5f, size);
        }

        public static Rect GetContentArea(Canvas canvas)
        {
            var area = (RectTransform)canvas.rootCanvas.transform;
            Rect available = area.rect;
            // Canvas 자체는 전체 화면이므로 실제 BottomPanel의 위쪽 경계를 사용합니다.
            foreach (var candidate in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene != canvas.gameObject.scene || !candidate.isActiveAndEnabled ||
                    (candidate.name != "BottomNavigationCanvas" && candidate.name != "BottomNavigationBar")) continue;
                var panel = candidate.transform.Find("BottomPanel") as RectTransform;
                if (panel == null || !panel.gameObject.activeInHierarchy) continue;
                var corners = new Vector3[4]; panel.GetWorldCorners(corners);
                float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
                Camera fromCamera = candidate.renderMode == RenderMode.ScreenSpaceOverlay ? null : candidate.worldCamera;
                Camera toCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                foreach (var corner in corners)
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(fromCamera, corner);
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, point, toCamera, out var local)) continue;
                    bottom = Mathf.Min(bottom, local.y); top = Mathf.Max(top, local.y);
                }
                if (bottom <= area.rect.yMin + 1 && top > area.rect.yMin)
                    available.yMin = Mathf.Max(available.yMin, Mathf.Clamp(top, area.rect.yMin, area.rect.yMax));
            }
            return available;
        }
    }
}
