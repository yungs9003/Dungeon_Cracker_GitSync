using UnityEngine;

namespace Dungeon.Player
{
    public class TrapZoneHighlight : MonoBehaviour
    {
        [Header("Highlight Visual")]
        [SerializeField] private GameObject highlightVisual;

        private bool isHighlighted;

        private void Awake()
        {
            SetHighlighted(false);
        }

        public void SetHighlighted(bool value)
        {
            if (isHighlighted == value) return;
            isHighlighted = value;

            if (highlightVisual != null)
                highlightVisual.SetActive(value);
        }

        private void OnDisable()
        {
            if (highlightVisual != null)
                highlightVisual.SetActive(false);

            isHighlighted = false;
        }
    }
}