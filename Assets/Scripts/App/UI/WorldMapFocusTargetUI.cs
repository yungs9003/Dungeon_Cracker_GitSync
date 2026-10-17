using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Dungeon.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class WorldMapFocusTargetUI : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private WorldMapPanZoomUI mapView;
        [SerializeField, Min(1f)] private float focusZoom = 1.6f;
        [Tooltip("확대 이동이 끝난 뒤 실행합니다. 기존 던전 팝업 호출을 이곳에 연결합니다.")]
        [SerializeField] private UnityEvent onFocused = new();
        public UnityEvent OnFocused => onFocused;

        public void Configure(WorldMapPanZoomUI view) => mapView = view;
        public void OnPointerDown(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) mapView?.StopInertia(); }
        public void Focus()
        {
            if (mapView == null || !mapView.CanClickPoint) return;
            mapView.FocusOn((RectTransform)transform, focusZoom, () => onFocused.Invoke());
        }
    }
}
