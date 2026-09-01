using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dungeon.UI
{
    /// <summary>
    /// 콘텐츠 영역의 포인터 드래그만 차단하는 ScrollRect입니다.
    /// 마우스 휠과 연결된 Scrollbar 조작은 ScrollRect 기본 동작을 그대로 사용합니다.
    /// </summary>
    [AddComponentMenu("UI/No Content Drag Scroll Rect")]
    public sealed class NoContentDragScrollRect : ScrollRect
    {
        public override void OnBeginDrag(PointerEventData eventData)
        {
        }

        public override void OnDrag(PointerEventData eventData)
        {
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
        }
    }
}
