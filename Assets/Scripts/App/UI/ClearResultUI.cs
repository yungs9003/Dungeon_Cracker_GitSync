using UnityEngine;
using TMPro;
using Dungeon.Core;

namespace Dungeon.UI
{
    public class ClearResultUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text dummyText;

        private void Start()
        {
            int gold = SaveFileManager.GetGold();
            int dummy1 = SaveFileManager.GetDummy1();

            if (goldText != null)
                goldText.text = $"Gold: {gold}";

            if (dummyText != null)
                dummyText.text = $"Dummy1: {dummy1}";
        }
    }
}