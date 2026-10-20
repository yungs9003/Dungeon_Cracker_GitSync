using System;

namespace Dungeon.Core
{
    public enum DungeonClearRank { None, C, B, A, S }

    public readonly struct DungeonClearRankResult
    {
        public readonly decimal HitScore, TimeScore, TotalScore;
        public readonly DungeonClearRank Rank;

        public DungeonClearRankResult(decimal hitScore, decimal timeScore)
        {
            HitScore = hitScore;
            TimeScore = timeScore;
            TotalScore = hitScore * .6m + timeScore * .4m;
            Rank = TotalScore >= 90m ? DungeonClearRank.S :
                TotalScore >= 75m ? DungeonClearRank.A : TotalScore >= 50m ? DungeonClearRank.B : DungeonClearRank.C;
        }
    }

    /// <summary>이번 클리어의 실제 시간/피격을 평가합니다. 서로 다른 플레이의 최고 기록은 섞지 않습니다.</summary>
    public static class DungeonClearRankCalculator
    {
        public static bool TryCalculate(DungeonDefinition dungeon, int hitCount, int seconds, out DungeonClearRankResult result)
        {
            result = default;
            if (dungeon == null || dungeon.rank_reference_hit_count <= 0 || dungeon.rank_reference_time_seconds <= 0)
                return false;
            decimal hits = Math.Max(0, hitCount), time = Math.Max(0, seconds);
            decimal baseHits = dungeon.rank_reference_hit_count, baseTime = dungeon.rank_reference_time_seconds;
            decimal hitScore = 100m * Clamp01((3m * baseHits - hits) / (2m * baseHits));
            decimal timeScore = 100m * Clamp01((2m * baseTime - time) / baseTime);
            result = new DungeonClearRankResult(hitScore, timeScore);
            return true;
        }

        private static decimal Clamp01(decimal value) => Math.Min(1m, Math.Max(0m, value));
    }

    public static class DungeonTime
    {
        public static int WholeSeconds(double value, int fallback = 0) =>
            double.IsNaN(value) || double.IsInfinity(value) ? fallback : (int)Math.Min(int.MaxValue, Math.Max(0d, Math.Floor(value)));

        public static string Format(double seconds)
        {
            int whole = WholeSeconds(seconds);
            return $"{whole / 60:00}:{whole % 60:00}";
        }
    }
}
