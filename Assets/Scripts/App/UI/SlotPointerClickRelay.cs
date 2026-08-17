using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Dungeon.UI
{
    /// <summary>
    /// Distinguishes single-left, double-left, and right clicks for UI slots.
    /// The single click is delayed briefly so it is not fired before a double click.
    /// </summary>
    public class SlotPointerClickRelay : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField, Min(0f)]
        private float singleClickDelay = 0.24f;

        public event Action LeftClicked;
        public event Action DoubleLeftClicked;
        public event Action RightClicked;

        private Coroutine pendingSingleClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                CancelPendingSingleClick();
                RightClicked?.Invoke();
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            if (eventData.clickCount >= 2)
            {
                CancelPendingSingleClick();
                DoubleLeftClicked?.Invoke();
                return;
            }

            CancelPendingSingleClick();

            if (singleClickDelay <= 0f)
            {
                LeftClicked?.Invoke();
                return;
            }

            pendingSingleClick = StartCoroutine(InvokeSingleClickAfterDelay());
        }

        private IEnumerator InvokeSingleClickAfterDelay()
        {
            yield return new WaitForSecondsRealtime(singleClickDelay);
            pendingSingleClick = null;
            LeftClicked?.Invoke();
        }

        private void OnDisable()
        {
            CancelPendingSingleClick();
        }

        private void CancelPendingSingleClick()
        {
            if (pendingSingleClick == null)
                return;

            StopCoroutine(pendingSingleClick);
            pendingSingleClick = null;
        }
    }
}
