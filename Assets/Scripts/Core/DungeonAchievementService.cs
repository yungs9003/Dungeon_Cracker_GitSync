using System;
using System.Collections.Generic;

namespace Dungeon.Core
{
    public enum DungeonAchievementKind { Hits, Time, Disarm }

    public readonly struct AdventureRecordSummary
    {
        public readonly int TotalDungeons, ClearedDungeons, AchievedChallenges;
        public int TotalChallenges => TotalDungeons * 3;
        public string ClearedDisplay => $"{ClearedDungeons}/{TotalDungeons}";
        public string AchievementDisplay => $"{AchievedChallenges}/{TotalChallenges}";
        // 25/50/75%는 상위 등급에 포함하며, 등록된 과제가 없으면 0%인 C입니다.
        public string AdventureRank
        {
            get
            {
                long total = (long)TotalDungeons * 3;
                long achieved = Math.Max(0, AchievedChallenges);
                return total <= 0 ? "C" : achieved * 4 >= total * 3 ? "S" :
                    achieved * 2 >= total ? "A" : achieved * 4 >= total ? "B" : "C";
            }
        }
        public AdventureRecordSummary(int total, int cleared, int achieved)
        { TotalDungeons = total; ClearedDungeons = cleared; AchievedChallenges = achieved; }
    }

    /// <summary>던전 DB 기준과 클리어한 플레이의 독립 최고 기록으로 세 과제를 계산합니다.</summary>
    public static class DungeonAchievementService
    {
        public const string HitsId = "low_hits", TimeId = "fast_clear", DisarmId = "disarm_60_percent";
        public static string Id(DungeonAchievementKind kind) => kind switch
        {
            DungeonAchievementKind.Hits => HitsId,
            DungeonAchievementKind.Time => TimeId,
            DungeonAchievementKind.Disarm => DisarmId,
            _ => ""
        };

        public static bool TryKind(string id, out DungeonAchievementKind kind)
        {
            kind = id switch
            {
                HitsId => DungeonAchievementKind.Hits,
                TimeId => DungeonAchievementKind.Time,
                DisarmId => DungeonAchievementKind.Disarm,
                _ => (DungeonAchievementKind)(-1)
            };
            return (int)kind >= 0;
        }

        // 정수 연산으로 정확히 ceil(total * 0.6). int.MaxValue에서도 넘치지 않습니다.
        public static int RequiredDisarms(int total) => total < 0 ? -1 : (int)(((long)total * 3 + 4) / 5);

        public static bool IsAchieved(StatusSaveData data, DungeonDefinition dungeon, DungeonAchievementKind kind)
        {
            if (data == null || dungeon == null || string.IsNullOrWhiteSpace(dungeon.sceneName) ||
                !DungeonProgressService.TryGetClearedRecord(data, dungeon.sceneName, out var record)) return false;
            return kind switch
            {
                DungeonAchievementKind.Hits => dungeon.rank_reference_hit_count >= 0 && record.best_hit_count <= dungeon.rank_reference_hit_count,
                DungeonAchievementKind.Time => dungeon.rank_reference_time_seconds > 0 && record.best_clear_time_seconds <= dungeon.rank_reference_time_seconds,
                DungeonAchievementKind.Disarm => dungeon.destructible_trap_count >= 0 && record.max_broken_traps >= RequiredDisarms(dungeon.destructible_trap_count),
                _ => false
            };
        }

        public static AdventureRecordSummary Summarize(StatusSaveData data, IEnumerable<DungeonDefinition> dungeons)
        {
            int total = 0, cleared = 0, achieved = 0;
            var scenes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dungeon in dungeons ?? Array.Empty<DungeonDefinition>())
            {
                if (dungeon == null || string.IsNullOrWhiteSpace(dungeon.sceneName) ||
                    !scenes.Add(DungeonProgressService.SceneKey(dungeon.sceneName))) continue;
                total++;
                if (data != null && DungeonProgressService.TryGetClearedRecord(data, dungeon.sceneName, out _)) cleared++;
                foreach (DungeonAchievementKind kind in Enum.GetValues(typeof(DungeonAchievementKind)))
                    if (IsAchieved(data, dungeon, kind)) achieved++;
            }
            return new AdventureRecordSummary(total, cleared, achieved);
        }

        public static void RefreshSummary(StatusSaveData data, IEnumerable<DungeonDefinition> dungeons)
        {
            var summary = Summarize(data, dungeons);
            data.clear_num_info = summary.ClearedDisplay;
            data.achievement = summary.AchievementDisplay;
            data.player_rank = summary.AdventureRank;
        }

        public static bool IsClaimed(StatusSaveData data, string scene, string id) =>
            data?.guild_achievement_claims != null && data.guild_achievement_claims.TryGetValue(DungeonProgressService.SceneKey(scene), out var claims)
            && claims != null && claims.Contains(id);

        /// <summary>StatusSaveFile의 원자적 갱신 안에서만 호출해 보상과 수령 표식을 함께 저장합니다.</summary>
        public static bool TryClaim(StatusSaveData data, DungeonDefinition dungeon, string id)
        {
            if (!TryKind(id, out var kind) || !IsAchieved(data, dungeon, kind) || IsClaimed(data, dungeon.sceneName, id) ||
                dungeon.achievement_reward_gold < 0 || data.gold > int.MaxValue - dungeon.achievement_reward_gold) return false;
            data.guild_achievement_claims ??= new Dictionary<string, List<string>>();
            string scene = DungeonProgressService.SceneKey(dungeon.sceneName);
            if (!data.guild_achievement_claims.TryGetValue(scene, out var claims))
                data.guild_achievement_claims[scene] = claims = new List<string>();
            claims.Add(id);
            data.gold += dungeon.achievement_reward_gold;
            return true;
        }
    }
}
