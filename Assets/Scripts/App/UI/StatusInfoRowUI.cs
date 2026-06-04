using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dungeon.UI
{
    public class StatusInfoRowUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text valueText;

        public void Set(Sprite icon, string label, int value)
        {
            if (iconImage != null)
                iconImage.sprite = icon;

            if (labelText != null)
                labelText.text = label;

            if (valueText != null)
                valueText.text = value.ToString();
        }

        public void SetValue(int value)
        {
            if (valueText != null)
                valueText.text = value.ToString();
        }
    }
}