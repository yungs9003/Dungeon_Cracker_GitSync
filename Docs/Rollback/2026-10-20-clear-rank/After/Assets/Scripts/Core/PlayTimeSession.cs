using System;
using System.Globalization;

namespace Dungeon.Core
{
    /// <summary>시계만 주입받는 메모리 타이머. 파일 쓰기와 Unity 생명주기는 StatusSaveFile이 담당합니다.</summary>
    public sealed class PlayTimeSession
    {
        private double lastRealtime, lastScaledTime, remainder, runSeconds, runCompensation, retryAfter;
        private long minutes, savedMinutes;
        private string runId;
        private bool countingDungeon;
        private int observedRunSeconds;

        public PlayTimeSession(StatusSaveData data, double realtime, double scaledTime, string sceneName)
        {
            PlayTimeData.Normalize(data);
            minutes = savedMinutes = data.play_time_minutes;
            remainder = data.play_time_remainder_seconds;
            lastRealtime = realtime;
            lastScaledTime = scaledTime;
            BindRun(data, sceneName);
        }

        public void Sample(StatusSaveData data, double realtime, double scaledTime, string sceneName)
        {
            double realDelta = Elapsed(realtime, lastRealtime);
            double scaledDelta = Elapsed(scaledTime, lastScaledTime);
            lastRealtime = Math.Max(lastRealtime, realtime);
            lastScaledTime = Math.Max(lastScaledTime, scaledTime);

            double seconds = remainder + realDelta;
            long wholeMinutes = (long)Math.Min(Math.Floor(seconds / 60d), long.MaxValue - minutes);
            minutes += wholeMinutes;
            remainder = seconds % 60d;
            data.play_time_minutes = minutes;
            data.play_time_remainder_seconds = remainder;
            data.play_time = PlayTimeData.Format(minutes);

            // 트랜잭션 교체/재개/완료 때 다른 런으로 지난 프레임의 시간을 넘기지 않습니다.
            if (countingDungeon && runId == data.active_run?.run_id &&
                DungeonProgressService.TryGetActiveCheckpoint(data, out var checkpoint) &&
                !checkpoint.completed && !data.active_run.pending_clear)
            {
                if (data.clear_time_seconds != observedRunSeconds)
                {
                    runSeconds = data.clear_time_seconds;
                    runCompensation = 0;
                }
                // 작은 프레임 시간을 유지하고 완성된 초만 공개/저장합니다.
                double adjusted = scaledDelta - runCompensation;
                double updated = runSeconds + adjusted;
                runCompensation = (updated - runSeconds) - adjusted;
                runSeconds = Math.Min(int.MaxValue, updated);
                data.clear_time_seconds = DungeonTime.WholeSeconds(runSeconds);
                observedRunSeconds = data.clear_time_seconds;
            }
            BindRun(data, sceneName);
        }

        /// <summary>데이터 교체 직후, 이미 측정한 시간을 다시 더하지 않고 대상 런만 변경합니다.</summary>
        public void BindRun(StatusSaveData data, string sceneName)
        {
            string nextRun = data.active_run?.run_id;
            if (runId != nextRun || data.clear_time_seconds != observedRunSeconds)
            {
                runSeconds = data.clear_time_seconds;
                runCompensation = 0;
            }
            runId = nextRun;
            observedRunSeconds = data.clear_time_seconds;
            countingDungeon = !string.IsNullOrEmpty(sceneName) &&
                sceneName.StartsWith("DUNGEON_", StringComparison.OrdinalIgnoreCase) &&
                data.active_run?.sceneName == DungeonProgressService.SceneKey(sceneName) &&
                !data.active_run.pending_clear &&
                DungeonProgressService.TryGetActiveCheckpoint(data, out var checkpoint) && !checkpoint.completed;
        }

        public bool IsSaveDue(double realtime) => minutes > savedMinutes && realtime >= retryAfter;
        public void Saved() { savedMinutes = minutes; retryAfter = 0; }
        public void SaveFailed(double realtime) { retryAfter = realtime + 5d; }

        private static double Elapsed(double now, double before) =>
            double.IsNaN(now) || double.IsInfinity(now) ? 0 : Math.Max(0, now - before);
    }

    public static class PlayTimeData
    {
        public static string Format(long minutes) =>
            (Math.Max(0, minutes) / 60).ToString(CultureInfo.InvariantCulture) + ":" +
            (Math.Max(0, minutes) % 60).ToString("00", CultureInfo.InvariantCulture);

        public static bool TryParseLegacy(string value, out long minutes)
        {
            minutes = 0;
            if (string.IsNullOrWhiteSpace(value)) return true;
            string[] parts = value.Trim().Split(':');
            if (parts.Length != 2 ||
                !long.TryParse(parts[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out long hours) ||
                !int.TryParse(parts[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int minute) ||
                minute > 59 || hours > (long.MaxValue - minute) / 60) return false;
            minutes = hours * 60 + minute;
            return true;
        }

        public static void Normalize(StatusSaveData data)
        {
            data.play_time_legacy_unparsed ??= "";
            if (data.play_time_minutes < 0)
            {
                if (!TryParseLegacy(data.play_time, out data.play_time_minutes))
                    data.play_time_legacy_unparsed = data.play_time ?? "";
            }
            data.play_time_minutes = Math.Max(0, data.play_time_minutes);
            double remainder = data.play_time_remainder_seconds;
            data.play_time_remainder_seconds = double.IsNaN(remainder) || double.IsInfinity(remainder) || remainder < 0
                ? 0 : remainder % 60d;
            data.play_time = Format(data.play_time_minutes);
        }
    }
}
