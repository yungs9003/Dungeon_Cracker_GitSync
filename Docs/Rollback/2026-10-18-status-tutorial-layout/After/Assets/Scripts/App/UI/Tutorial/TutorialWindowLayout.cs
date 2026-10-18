using UnityEngine;

namespace Dungeon.UI.Tutorial
{
    /// <summary>상태창과 같은 기준 크기의 슬라이드와 바깥쪽 조작 버튼을 배치합니다.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class TutorialWindowLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform slide, previous, next, close;
        private bool applying;
        public void Configure(RectTransform image, RectTransform back, RectTransform forward, RectTransform dismiss)
        { slide = image; previous = back; next = forward; close = dismiss; RefreshLayout(); }
        private void OnEnable() => RefreshLayout();
        private void OnRectTransformDimensionsChange() => RefreshLayout();
        private void LateUpdate() => RefreshLayout();

        public void RefreshLayout()
        {
            if (applying || slide == null || previous == null || next == null || close == null) return;
            var body = (RectTransform)transform;
            Rect area = body.rect;
            if (area.width <= 0 || area.height <= 128) return;
            applying = true;
            try
            {
                // 시안처럼 닫기 버튼 아래에 슬라이드를 놓습니다. 하단 바보다 작은128px 상단 공간을 사용합니다.
                Rect content = area; content.yMax -= 128;
                Vector2 size = StatusWindowLayout.ReferenceSize;
                float scale = Mathf.Min(1, content.width / StatusWindowLayout.ReferenceScreenWidth, content.height / size.y);
                size *= scale;
                Place(slide, content.center, size);
                float margin = Mathf.Max(0, (area.width - size.x) * .5f);
                float inset = Mathf.Min(48, margin * .5f);
                float arrowScale = Mathf.Clamp(margin / 100, .5f, 1);
                Place(previous, new Vector2(area.xMin + inset, content.center.y), new Vector2(168, 231) * arrowScale);
                Place(next, new Vector2(area.xMax - inset, content.center.y), new Vector2(168, 231) * arrowScale);
                close.anchorMin = close.anchorMax = close.pivot = new Vector2(0, 1);
                close.sizeDelta = new Vector2(308, 166);
                // PNG에 포함된 그림자 여백을 감안해 실제 회색 버튼을 화면의 좌측 상단에 맞춥니다.
                close.anchoredPosition = new Vector2(-20, 36);
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
