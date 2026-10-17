using UnityEngine;

namespace Dungeon.UI.Guild
{
    /// <summary>자식의 시안 좌표는 유지하고 페이지 전체를 축소/확대합니다. 하단 공통 메뉴와 아래 모서리를 맞춥니다.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class GuildReferenceLayout : MonoBehaviour
    {
        [SerializeField] private Vector2 referenceSize = new(1920f, 1080f);
        private bool applying;
        private void OnEnable() => RefreshLayout();
        private void OnRectTransformDimensionsChange() => RefreshLayout();
        private void OnTransformParentChanged() => RefreshLayout();
        private void LateUpdate() => RefreshLayout();

        public void RefreshLayout()
        {
            if (applying) return;
            var rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            if (parent == null || referenceSize.x <= 0f || referenceSize.y <= 0f) return;
            Vector2 available = parent.rect.size;
            if (available.x <= 0f || available.y <= 0f) return;
            float scale = Mathf.Min(available.x / referenceSize.x, available.y / referenceSize.y);
            applying = true;
            try
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = referenceSize;
                rect.localScale = new Vector3(scale, scale, 1f);
            }
            finally { applying = false; }
        }
    }
}
