using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Dungeon.Core;
using Dungeon.Manager;

namespace Dungeon.UI
{
    public class ClearResultUI : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField] private TMP_Text brokenTrapsText;
        [SerializeField] private TMP_Text gettingGoldText;
        [SerializeField] private TMP_Text totalGoldText;
        [SerializeField] private TMP_Text clearTimeText;
        [SerializeField] private TMP_Text hitCountText;

        [Header("Clear Rank")]
        [SerializeField] private Image clearRankImage;
        [SerializeField] private Sprite sRankSprite;
        [SerializeField] private Sprite aRankSprite;
        [SerializeField] private Sprite bRankSprite;
        [SerializeField] private Sprite cRankSprite;
        public DungeonClearRank DisplayedRank { get; private set; }

        [Header("Format")]
        [SerializeField] private string brokenTrapsFormat = "{0}개";
        [SerializeField] private string gettingGoldFormat = "{0} G";
        [SerializeField] private string totalGoldFormat = "{0} G";
        [SerializeField] private string clearTimeFormat = "{0}";
        [SerializeField] private string hitCountFormat = "{0}회";

        [Header("Leave Clear Scene")]
        [SerializeField] private string nextSceneName = "MainMenu";

        private void Start()
        {
            Refresh();
        }

        public void Refresh()
        {
            DungeonRunTracker2D tracker = DungeonRunTracker2D.Instance;
            if (tracker != null)
                tracker.FlushTimeToSave();

            StatusSaveFile saveFile = StatusSaveFile.Instance;

            if (saveFile == null)
            {
                Debug.LogWarning("[ClearResultUI] StatusSaveFile.Instance is missing.");
                SetFallbackTexts();
                return;
            }

            int brokenTraps = saveFile.GetBrokenTraps();
            // 씬 진입 처리의 저장 실패는 여기서 재시도하되 같은 run_id는 다시 집계하지 않습니다.
            saveFile.TryFinalizeDungeonClear();
            int gettingGold = DungeonProgressService.HasCompletedActiveRun(saveFile.Data) ? saveFile.ClaimClearRewardGold() : 0;
            int totalGold = saveFile.Data.gold;

            float clearSeconds = saveFile.GetClearTimeSeconds();
            int hitCount = saveFile.GetHitCount();

            if (brokenTrapsText != null)
                brokenTrapsText.text = string.Format(brokenTrapsFormat, brokenTraps);

            if (gettingGoldText != null)
                gettingGoldText.text = string.Format(gettingGoldFormat, gettingGold);

            if (totalGoldText != null)
                totalGoldText.text = string.Format(totalGoldFormat, totalGold);

            if (clearTimeText != null)
                clearTimeText.text = string.Format(clearTimeFormat, FormatTime(clearSeconds));

            if (hitCountText != null)
                hitCountText.text = string.Format(hitCountFormat, hitCount);

            RefreshRank(saveFile.Data);
        }

        private string FormatTime(float seconds) => DungeonTime.Format(seconds);

        private void RefreshRank(StatusSaveData data)
        {
            DungeonClearRank rank = DungeonClearRank.None;
            if (DungeonProgressService.HasCompletedActiveRun(data) &&
                GameDatabase.TryFindDungeonBySceneName(data.active_run.sceneName, out var dungeon) &&
                DungeonClearRankCalculator.TryCalculate(dungeon, data.hit_count, data.clear_time_seconds, out var result))
                rank = result.Rank;
            ShowRank(rank);
        }

        private void ShowRank(DungeonClearRank rank)
        {
            DisplayedRank = rank;
            if (clearRankImage == null) return;
            clearRankImage.sprite = rank switch
            {
                DungeonClearRank.S => sRankSprite,
                DungeonClearRank.A => aRankSprite,
                DungeonClearRank.B => bRankSprite,
                DungeonClearRank.C => cRankSprite,
                _ => null
            };
            clearRankImage.enabled = clearRankImage.sprite != null;
        }

        private void SetFallbackTexts()
        {
            if (brokenTrapsText != null)
                brokenTrapsText.text = string.Format(brokenTrapsFormat, 0);

            if (gettingGoldText != null)
                gettingGoldText.text = string.Format(gettingGoldFormat, 0);

            if (totalGoldText != null)
                totalGoldText.text = string.Format(totalGoldFormat, 0);

            if (clearTimeText != null)
                clearTimeText.text = string.Format(clearTimeFormat, "00:00");

            if (hitCountText != null)
                hitCountText.text = string.Format(hitCountFormat, 0);
            ShowRank(DungeonClearRank.None);
        }

        public void LeaveClearScene()
        {
            StatusSaveFile saveFile = StatusSaveFile.Instance;

            if (saveFile != null)
                saveFile.ResetRunResultStats();

            if (!string.IsNullOrEmpty(nextSceneName))
                SceneManager.LoadScene(nextSceneName);
        }
    }
}
