using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dungeon.UI.Guild
{
    public sealed class GuildPageUI : MonoBehaviour
    {
        [SerializeField] private GuildCatalog catalog;
        [SerializeField] private GuildDungeonCaseUI casePrefab;
        [SerializeField] private GuildAchievementRowUI achievementPrefab;
        [SerializeField] private RectTransform caseContent;
        [SerializeField] private RectTransform achievementContent;
        [SerializeField] private ScrollRect achievementScroll;
        [SerializeField] private Button commentButton;
        [SerializeField] private TMP_Text commentLabel;
        [SerializeField] private TMP_Text caseLabel;
        [SerializeField] private TMP_Text dungeonNameLabel;
        private readonly List<GuildDungeonCaseUI> cases = new();
        private readonly List<GuildAchievementRowUI> rows = new();
        private IGuildAchievementProgressSource progress;
        private GuildDungeonDefinition selectedDungeon;
        private int commentIndex;
        public GuildCatalog Catalog => catalog;
        public string SelectedDungeonId => selectedDungeon?.id;
        public int CommentIndex => commentIndex;

        private void OnEnable() { if (commentButton != null) commentButton.onClick.AddListener(NextComment); }
        private void OnDisable() { if (commentButton != null) commentButton.onClick.RemoveListener(NextComment); }
        private void Start() => Initialize();

        public void Configure(GuildCatalog data, GuildDungeonCaseUI caseTemplate, GuildAchievementRowUI rowTemplate,
            RectTransform casesRoot, RectTransform rowsRoot, ScrollRect rowsScroll, Button comment,
            TMP_Text commentText, TMP_Text numberText, TMP_Text nameText)
        {
            catalog = data; casePrefab = caseTemplate; achievementPrefab = rowTemplate;
            caseContent = casesRoot; achievementContent = rowsRoot; achievementScroll = rowsScroll;
            commentButton = comment; commentLabel = commentText; caseLabel = numberText; dungeonNameLabel = nameText;
        }

        /// <summary>씬 편집 미리보기와 런타임 모두 같은 카탈로그/프리팹으로 목록을 구성합니다.</summary>
        public void Initialize()
        {
            if (catalog == null) return;
            progress ??= new GuildPreviewProgressSource(catalog);
            Clear(caseContent); cases.Clear();
            foreach (var definition in catalog.dungeons)
            {
                if (definition == null) continue;
                var view = Instantiate(casePrefab, caseContent);
                view.name = $"Case_{definition.caseNumber:00}";
                view.Bind(this, definition); cases.Add(view);
            }
            ShowComment();
            var initial = catalog.FindDungeon(catalog.initialDungeonId);
            if (initial == null && cases.Count > 0) initial = catalog.FindDungeon(cases[0].DungeonId);
            if (initial != null) SelectDungeon(initial.id);
        }

        public void BindProgressSource(IGuildAchievementProgressSource source)
        {
            progress = source ?? throw new ArgumentNullException(nameof(source));
            RefreshProgress();
        }

        public void NextComment()
        {
            if (catalog == null || catalog.comments.Length == 0) return;
            commentIndex = (commentIndex + 1) % catalog.comments.Length;
            ShowComment();
        }

        private void ShowComment()
        {
            if (commentLabel == null) return;
            if (catalog.comments.Length == 0) { commentLabel.text = string.Empty; return; }
            commentIndex = Mathf.Clamp(commentIndex, 0, catalog.comments.Length - 1);
            commentLabel.text = catalog.comments[commentIndex];
        }

        public void SelectDungeon(string dungeonId)
        {
            var definition = catalog.FindDungeon(dungeonId);
            if (definition == null) return;
            selectedDungeon = definition;
            foreach (var view in cases) view.SetSelected(view.DungeonId == dungeonId);
            caseLabel.text = $"CASE NO.{definition.caseNumber:00}";
            dungeonNameLabel.text = definition.dungeonName;
            Clear(achievementContent); rows.Clear();
            for (int i = 0; i < definition.achievements.Length; i++)
            {
                var achievement = definition.achievements[i];
                if (achievement == null) continue;
                var row = Instantiate(achievementPrefab, achievementContent);
                row.name = $"Achievement_{achievement.id}";
                row.Bind(this, achievement, i); rows.Add(row);
            }
            RefreshProgress();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(achievementContent);
            achievementScroll.StopMovement(); achievementScroll.verticalNormalizedPosition = 1f;
        }

        public void RefreshProgress()
        {
            if (selectedDungeon == null || progress == null) return;
            foreach (var row in rows) row.SetState(progress.GetState(selectedDungeon.id, row.AchievementId));
        }

        public void TryVerify(GuildAchievementRowUI row)
        {
            if (row == null || !rows.Contains(row) || selectedDungeon == null || progress == null) return;
            if (progress.GetState(selectedDungeon.id, row.AchievementId) != GuildAchievementState.Verifiable) return;
            // 향후 실제 저장 처리가 성공한 경우에만 인증 표시로 전환합니다.
            if (progress.TryClaim(selectedDungeon.id, row.AchievementId)) RefreshProgress();
        }

        private static void Clear(RectTransform content)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i).gameObject;
                if (Application.isPlaying) { child.SetActive(false); Destroy(child); }
                else DestroyImmediate(child);
            }
        }
    }
}
