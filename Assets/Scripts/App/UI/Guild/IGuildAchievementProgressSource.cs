using System.Collections.Generic;

namespace Dungeon.UI.Guild
{
    /// <summary>
    /// 향후 SaveData 어댑터는 클리어 조건을 조회하고, TryClaim에서 조건 재확인·보상·수령 기록을 함께 저장합니다.
    /// 저장에 성공한 경우에만 true를 반환하며, 이미 수령한 항목은 다시 지급하지 않습니다.
    /// </summary>
    public interface IGuildAchievementProgressSource
    {
        GuildAchievementState GetState(string dungeonId, string achievementId);
        bool TryClaim(string dungeonId, string achievementId);
    }

    /// <summary>화면 확인 전용. 보상이나 실제 세이브를 변경하지 않고 현재 화면 세션에만 상태를 보관합니다.</summary>
    public sealed class GuildPreviewProgressSource : IGuildAchievementProgressSource
    {
        private readonly Dictionary<(string, string), GuildAchievementState> states = new();

        public GuildPreviewProgressSource(GuildCatalog catalog)
        {
            if (catalog == null) return;
            foreach (var dungeon in catalog.dungeons)
            {
                if (dungeon == null || string.IsNullOrWhiteSpace(dungeon.id)) continue;
                foreach (var achievement in dungeon.achievements)
                    if (achievement != null && !string.IsNullOrWhiteSpace(achievement.id))
                        states[(dungeon.id, achievement.id)] = achievement.previewState;
            }
        }

        public GuildAchievementState GetState(string dungeonId, string achievementId) =>
            states.TryGetValue((dungeonId, achievementId), out var state) ? state : GuildAchievementState.Locked;

        public bool TryClaim(string dungeonId, string achievementId)
        {
            if (GetState(dungeonId, achievementId) != GuildAchievementState.Verifiable) return false;
            states[(dungeonId, achievementId)] = GuildAchievementState.Certified;
            return true;
        }
    }
}
