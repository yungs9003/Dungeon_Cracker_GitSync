using System;
using UnityEngine;

namespace Dungeon.UI.Guild
{
    public enum GuildAchievementState { Locked, Verifiable, Certified }

    [Serializable]
    public sealed class GuildAchievementDefinition
    {
        public string id;
        [TextArea(1, 3)] public string condition;
        public string rewardLabel = "600G";
        [Tooltip("SaveData 연결 전 화면 시연용 상태입니다.")]
        public GuildAchievementState previewState;
    }

    [Serializable]
    public sealed class GuildDungeonDefinition
    {
        [Tooltip("향후 저장 기록과 연결할 고정 ID. 표시 번호나 이름과 분리합니다.")]
        public string id;
        public int caseNumber;
        public string dungeonName;
        public GuildAchievementDefinition[] achievements = Array.Empty<GuildAchievementDefinition>();
    }

    [CreateAssetMenu(menuName = "Dungeon/UI/Guild Catalog")]
    public sealed class GuildCatalog : ScriptableObject
    {
        [TextArea(2, 5)] public string[] comments = Array.Empty<string>();
        public string initialDungeonId;
        public GuildDungeonDefinition[] dungeons = Array.Empty<GuildDungeonDefinition>();

        public GuildDungeonDefinition FindDungeon(string id)
        {
            foreach (var dungeon in dungeons)
                if (dungeon != null && string.Equals(dungeon.id, id, StringComparison.Ordinal)) return dungeon;
            return null;
        }
    }
}
