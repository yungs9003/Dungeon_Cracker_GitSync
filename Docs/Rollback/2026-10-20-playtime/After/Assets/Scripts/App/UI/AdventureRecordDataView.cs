using System;
using System.Collections.Generic;
using System.Globalization;
using Dungeon.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dungeon.UI
{
    /// <summary>
    /// Adventure Record 화면의 동적 값을 StatusSaveFile에서 읽어 표시합니다.
    /// 페이지가 꺼져 있는 동안 변경된 값은 다음 OnEnable에서 다시 읽습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AdventureRecordDataView : MonoBehaviour
    {
        private const string Blue = "#6CACFF";
        private const string SlateBlue = "#6C88AF";

        [Header("Record Values")]
        [SerializeField] private TMP_Text playTimeText;
        [SerializeField] private TMP_Text clearedDungeonText;
        [SerializeField] private TMP_Text adventureCountText;
        [SerializeField] private TMP_Text guildAchievementText;

        [Header("Rank")]
        [SerializeField] private Image rankImage;
        [SerializeField] private TMP_Text rankFallbackText;
        [SerializeField] private Sprite sRankSprite;
        [SerializeField] private Sprite aRankSprite;
        [SerializeField] private Sprite bRankSprite;
        [SerializeField] private Sprite cRankSprite;

        private StatusSaveFile subscribedSaveFile;

        private void OnEnable()
        {
            SubscribeToSaveFile();
            Refresh();
        }

        private void OnDisable()
        {
            UnsubscribeFromSaveFile();
        }

        public void Configure(
            TMP_Text targetPlayTimeText,
            TMP_Text targetClearedDungeonText,
            TMP_Text targetAdventureCountText,
            TMP_Text targetGuildAchievementText,
            Image targetRankImage,
            TMP_Text targetRankFallbackText,
            Sprite targetSRankSprite,
            Sprite targetARankSprite,
            Sprite targetBRankSprite,
            Sprite targetCRankSprite)
        {
            playTimeText = targetPlayTimeText;
            clearedDungeonText = targetClearedDungeonText;
            adventureCountText = targetAdventureCountText;
            guildAchievementText = targetGuildAchievementText;
            rankImage = targetRankImage;
            rankFallbackText = targetRankFallbackText;
            sRankSprite = targetSRankSprite;
            aRankSprite = targetARankSprite;
            bRankSprite = targetBRankSprite;
            cRankSprite = targetCRankSprite;
        }

        public void Refresh()
        {
            if (StatusSaveFile.Instance != subscribedSaveFile)
                SubscribeToSaveFile();

            if (subscribedSaveFile != null)
                ApplyData(subscribedSaveFile.Data);
        }

        public void ApplyData(StatusSaveData data)
        {
            if (data == null)
            {
                ClearValues();
                return;
            }

            if (playTimeText != null)
                playTimeText.text = PlayTimeData.Format(data.play_time_minutes).Replace(":", " : ");

            if (clearedDungeonText != null)
                clearedDungeonText.text = FormatClearedDungeon(data.clear_num_info);

            if (adventureCountText != null)
            {
                adventureCountText.text =
                    $"{Mathf.Max(0, data.adventure_count).ToString(CultureInfo.InvariantCulture)} TIMES";
            }

            if (guildAchievementText != null)
                guildAchievementText.text = FormatAchievement(data.achievement);

            ApplyRank(data.player_rank);
        }

        private void SubscribeToSaveFile()
        {
            StatusSaveFile nextSaveFile = StatusSaveFile.Instance;

            if (subscribedSaveFile == nextSaveFile)
                return;

            UnsubscribeFromSaveFile();
            subscribedSaveFile = nextSaveFile;

            if (subscribedSaveFile != null)
                subscribedSaveFile.OnSaveDataChanged += ApplyData;
        }

        private void UnsubscribeFromSaveFile()
        {
            if (subscribedSaveFile != null)
                subscribedSaveFile.OnSaveDataChanged -= ApplyData;

            subscribedSaveFile = null;
        }

        private void ApplyRank(string rawRank)
        {
            string rank = string.IsNullOrWhiteSpace(rawRank)
                ? string.Empty
                : rawRank.Trim().ToUpperInvariant();
            Sprite rankSprite = GetRankSprite(rank);
            bool useImage = rankImage != null && rankSprite != null;

            if (rankImage != null)
            {
                if (useImage)
                    rankImage.sprite = rankSprite;

                rankImage.gameObject.SetActive(useImage);
            }

            if (rankFallbackText != null)
            {
                rankFallbackText.gameObject.SetActive(!useImage);
                rankFallbackText.text = rank;
            }
        }

        private Sprite GetRankSprite(string rank)
        {
            return rank switch
            {
                "S" => sRankSprite,
                "A" => aRankSprite,
                "B" => bRankSprite,
                "C" => cRankSprite,
                _ => null
            };
        }

        private static string FormatClearedDungeon(string rawValue)
        {
            string[] values = SplitTokens(rawValue);

            if (values.Length == 0)
                return string.Empty;

            if (values.Length == 1)
                return values[0];

            if (values.Length == 2)
                return $"<color={Blue}>{values[0]}</color> / {values[1]}";

            return
                $"<color={Blue}>{values[0]}</color> / " +
                $"<color={SlateBlue}>{values[1]}</color> / {values[2]}";
        }

        private static string FormatAchievement(string rawValue)
        {
            string[] values = SplitTokens(rawValue);

            if (values.Length == 0)
                return string.Empty;

            if (values.Length == 1)
                return values[0];

            return $"<color={Blue}>{values[0]}</color> / {values[1]}";
        }

        private static string JoinTokens(string rawValue, string separator)
        {
            string[] values = SplitTokens(rawValue);
            return values.Length == 0 ? string.Empty : string.Join(separator, values);
        }

        private static string[] SplitTokens(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return Array.Empty<string>();

            string[] split = rawValue.Split(
                new[] { ':', '/' },
                StringSplitOptions.RemoveEmptyEntries
            );
            List<string> values = new(split.Length);

            for (int i = 0; i < split.Length; i++)
            {
                string value = split[i].Trim();

                if (value.Length > 0)
                    values.Add(EscapeRichText(value));
            }

            return values.ToArray();
        }

        private void ClearValues()
        {
            if (playTimeText != null)
                playTimeText.text = string.Empty;
            if (clearedDungeonText != null)
                clearedDungeonText.text = string.Empty;
            if (adventureCountText != null)
                adventureCountText.text = string.Empty;
            if (guildAchievementText != null)
                guildAchievementText.text = string.Empty;

            ApplyRank(string.Empty);
        }

        private static string EscapeRichText(string value)
        {
            return value.Replace("<", "‹").Replace(">", "›");
        }
    }
}
