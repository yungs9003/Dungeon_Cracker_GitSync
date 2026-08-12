using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Dungeon.UI
{
    /// <summary>
    /// UI 슬롯의 마우스 클릭을 공통 처리합니다.
    ///
    /// 좌클릭 1회  -> LeftClicked
    /// 좌클릭 2회  -> DoubleLeftClicked
    /// 우클릭       -> RightClicked
    /// </summary>
    public class SlotPointerClickRelay :
        MonoBehaviour,
        IPointerClickHandler
    {
        public event Action LeftClicked;
        public event Action DoubleLeftClicked;
        public event Action RightClicked;

        public void OnPointerClick(
            PointerEventData eventData)
        {
            if (eventData.button ==
                PointerEventData.InputButton.Right)
            {
                RightClicked?.Invoke();
                return;
            }

            if (eventData.button !=
                PointerEventData.InputButton.Left)
            {
                return;
            }

            if (eventData.clickCount >= 2)
            {
                DoubleLeftClicked?.Invoke();
            }
            else
            {
                LeftClicked?.Invoke();
            }
        }
    }
}