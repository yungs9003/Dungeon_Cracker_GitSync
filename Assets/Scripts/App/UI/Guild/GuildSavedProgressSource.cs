using Dungeon.Core;

namespace Dungeon.UI.Guild
{
    /// <summary>매번 최신 저장을 조회하며 조건 달성과 한 번만 받는 보상을 구분합니다.</summary>
    public sealed class GuildSavedProgressSource : IGuildAchievementProgressSource
    {
        public GuildAchievementState GetState(string dungeonId, string achievementId)
        {
            var data = StatusSaveFile.Instance?.Data;
            if (!GameDatabase.TryFindDungeonBySceneName(dungeonId, out var dungeon) ||
                !DungeonAchievementService.TryKind(achievementId, out var kind) ||
                !DungeonAchievementService.IsAchieved(data, dungeon, kind)) return GuildAchievementState.Locked;
            return DungeonAchievementService.IsClaimed(data, dungeonId, achievementId)
                ? GuildAchievementState.Certified : GuildAchievementState.Verifiable;
        }

        public bool TryClaim(string dungeonId, string achievementId) =>
            StatusSaveFile.Instance != null && StatusSaveFile.Instance.TryClaimDungeonAchievement(dungeonId, achievementId);
    }
}
