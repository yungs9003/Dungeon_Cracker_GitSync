using System;
using System.Text.RegularExpressions;
using Dungeon.UI.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dungeon.UI
{
    /// <summary>던전 정보의 표시만 담당합니다. 저장 데이터 조회/수정은 하지 않습니다.</summary>
    public sealed class DungeonInfoPopupUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TMP_Text numberLabel, nameLabel, clearLabel, progressLabel;
        [SerializeField] private Button enterButton;
        [SerializeField] private TutorialLauncherUI helpButton;
        [SerializeField] private Sprite helpNormal, helpHover;
        public Button EnterButton => enterButton;

        public void Configure(TMP_Text number, TMP_Text title, TMP_Text clears, TMP_Text progress,
            Button enter, TutorialLauncherUI help, Sprite normal, Sprite hover)
        { numberLabel = number; nameLabel = title; clearLabel = clears; progressLabel = progress; enterButton = enter; helpButton = help; helpNormal = normal; helpHover = hover; }

        public void ConfigureHelp(TutorialSlides data, TutorialOverlayUI overlay)
            => helpButton.Configure(data, overlay, helpNormal, helpHover);

        public void Present(string dungeonId, string title, bool canEnter)
        {
            var match = Regex.Match(dungeonId ?? "", @"(\d+)$");
            string number = match.Success && int.TryParse(match.Value, out int value) ? value.ToString("00") : "--";
            numberLabel.text = "NO." + number;
            nameLabel.text = string.IsNullOrWhiteSpace(title) ? "던전" : title;
            enterButton.interactable = canEnter;
            // 던전별 저장 구조는 검토 후 연결합니다. 현재는 초기 표시값만 제공합니다.
            ApplyRecordDisplay(0, 0, 0);
        }

        /// <summary>향후 던전별 데이터 공급자가 호출할 표시 전용 함수입니다.</summary>
        public void ApplyRecordDisplay(int clearCount, int completedMaps, int totalMaps)
        {
            clearCount = Math.Max(0, clearCount); completedMaps = Math.Max(0, completedMaps);
            if (totalMaps > 0) completedMaps = Math.Min(completedMaps, totalMaps);
            string total = clearCount > 0 && totalMaps > 0 ? totalMaps.ToString() : "?";
            clearLabel.text = $"클리어 횟수 : {clearCount}회";
            progressLabel.text = $"현재 진행도 : {completedMaps}/{total}층";
        }

        // 카드 내부의 빈 부분을 눌렀을 때 바깥쪽 닫기 버튼으로 이벤트가 올라가지 않게 합니다.
        public void OnPointerClick(PointerEventData data) { }
    }
}
