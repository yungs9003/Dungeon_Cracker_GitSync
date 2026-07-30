using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Dungeon.UI
{
    /// <summary>
    /// Unity Button이 기본적으로 처리하지 않는 우클릭을 전달합니다.
    /// 슬롯 버튼 오브젝트에 직접 붙거나, UI 코드가 런타임에 자동으로 추가합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class SlotPointerClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public event Action RightClicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                RightClicked?.Invoke();
        }
    }
}
