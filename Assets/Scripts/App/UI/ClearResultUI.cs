using TMPro;
using UnityEngine;
using Dungeon.Core;

namespace Dungeon.UI
{
    public class ClearResultUI : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField] private TMP_Text brokenTrapsText;
        [SerializeField] private TMP_Text gettingGoldText;
        [SerializeField] private TMP_Text totalGoldText;

        [Header("Format")]
        [SerializeField] private string brokenTrapsFormat = "Broken Traps: {0}";
        [SerializeField] private string gettingGoldFormat = "Getting Gold: {0} G";
        [SerializeField] private string totalGoldFormat = "Total Gold: {0} G";

        private void Start()
        {
            Refresh();
        }

        public void Refresh()
        {
            StatusSaveFile saveFile = StatusSaveFile.Instance;

            if (saveFile == null)
            {
                Debug.LogWarning("[ClearResultUI] StatusSaveFile.Instance is missing.");
                SetFallbackTexts();
                return;
            }

            int brokenTraps = saveFile.GetBrokenTraps();

            int gettingGold = saveFile.ClaimClearRewardGold();

            int totalGold = saveFile.Data.gold;

            if (brokenTrapsText != null)
                brokenTrapsText.text = string.Format(brokenTrapsFormat, brokenTraps);

            if (gettingGoldText != null)
                gettingGoldText.text = string.Format(gettingGoldFormat, gettingGold);

            if (totalGoldText != null)
                totalGoldText.text = string.Format(totalGoldFormat, totalGold);
        }

        private void SetFallbackTexts()
        {
            if (brokenTrapsText != null)
                brokenTrapsText.text = string.Format(brokenTrapsFormat, 0);

            if (gettingGoldText != null)
                gettingGoldText.text = string.Format(gettingGoldFormat, 0);

            if (totalGoldText != null)
                totalGoldText.text = string.Format(totalGoldFormat, 0);
        }
    }
}