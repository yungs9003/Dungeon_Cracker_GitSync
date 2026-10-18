using UnityEngine;

namespace Dungeon.UI
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class DungeonInfoPopupLayout : MonoBehaviour
    {
        private bool applying;
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
                rect.sizeDelta = new Vector2(776, 484);
                float scale = Mathf.Min(1, content.width / 1920, content.height / 949);
                rect.localScale = new Vector3(scale, scale, 1);
                rect.position = area.TransformPoint(content.center);
            }
            finally { applying = false; }
        }
    }
}
