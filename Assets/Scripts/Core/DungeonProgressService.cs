using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Dungeon.Core
{
    [Serializable]
    public sealed class DungeonRecordData
    {
        public int clear_count;
        public int best_clear_time_seconds = 3599;
        public int best_hit_count = 399;
        public int max_broken_traps;
        public string last_committed_clear_run_id = "";
    }

    [Serializable]
    public sealed class DungeonCheckpointData
    {
        public int mapIndex, totalMaps;
        public bool resumeRequested, completed;
        public int broken_traps_snapshot;
        public string run_id = "";
        public int run_time_seconds;
        public int run_hit_count, run_broken_traps;
    }

    [Serializable]
    public sealed class ActiveDungeonRunData
    {
        public string sceneName = "", run_id = "";
        public bool pending_clear;
    }

    /// <summary>파일/UI/씬에 의존하지 않는 기록 상태 전이. 저장은 StatusSaveFile이 원자적으로 수행합니다.</summary>
    public static class DungeonProgressService
    {
        public static string SceneKey(string value)
        {
            value = value?.Trim() ?? "";
            var match = Regex.Match(value, @"^DUNGEON_(\d+)$", RegexOptions.IgnoreCase);
            return match.Success && int.TryParse(match.Groups[1].Value, out int number)
                ? "DUNGEON_" + number.ToString("000") : value;
        }

        public static DungeonRecordData GetRecord(StatusSaveData data, string sceneName)
        {
            string key = SceneKey(sceneName);
            return data != null && data.dungeon_records.TryGetValue(key, out var record)
                ? record : new DungeonRecordData();
        }

        /// <summary>길드 조건 검사에서는 기본값을 성과로 사용하지 않고 이 함수를 먼저 통과합니다.</summary>
        public static bool TryGetClearedRecord(StatusSaveData data, string sceneName, out DungeonRecordData record)
        {
            record = GetRecord(data, sceneName);
            return record.clear_count > 0;
        }

        public static bool TryGetActiveCheckpoint(StatusSaveData data, out DungeonCheckpointData checkpoint)
        {
            checkpoint = null;
            return data?.active_run != null && !string.IsNullOrEmpty(data.active_run.run_id) &&
                data.checkpoint.TryGetValue(SceneKey(data.active_run.sceneName), out checkpoint) &&
                checkpoint.run_id == data.active_run.run_id;
        }

        public static void CaptureActiveRun(StatusSaveData data)
        {
            if (!TryGetActiveCheckpoint(data, out var checkpoint) || checkpoint.completed) return;
            checkpoint.run_time_seconds = Math.Max(0, data.clear_time_seconds);
            checkpoint.run_hit_count = Math.Max(0, data.hit_count);
            checkpoint.run_broken_traps = Math.Max(0, data.broken_traps);
        }

        public static int Enter(StatusSaveData data, string sceneName, int totalMaps)
        {
            if (totalMaps <= 0 || string.IsNullOrWhiteSpace(sceneName)) throw new ArgumentException("Invalid dungeon/map count");
            CaptureActiveRun(data);
            string key = SceneKey(sceneName);
            if (!data.checkpoint.TryGetValue(key, out var checkpoint))
                data.checkpoint[key] = checkpoint = new DungeonCheckpointData();
            if (!data.dungeon_records.ContainsKey(key)) data.dungeon_records[key] = new DungeonRecordData();
            // 완료 표시 N/N은 재개 지점이 아닙니다. 별도 새 플레이 버튼 없이 다음 입장을 준비합니다.
            if (checkpoint.completed || string.IsNullOrEmpty(checkpoint.run_id))
                data.checkpoint[key] = checkpoint = new DungeonCheckpointData { run_id = Guid.NewGuid().ToString("N") };
            checkpoint.totalMaps = totalMaps;
            checkpoint.mapIndex = Math.Min(Math.Max(0, checkpoint.mapIndex), totalMaps);
            data.active_run = new ActiveDungeonRunData { sceneName = key, run_id = checkpoint.run_id };
            data.clear_time_seconds = checkpoint.run_time_seconds;
            data.hit_count = checkpoint.run_hit_count;
            data.broken_traps = checkpoint.resumeRequested ? checkpoint.broken_traps_snapshot : checkpoint.run_broken_traps;
            checkpoint.run_broken_traps = data.broken_traps;
            data.clear_reward_claimed = false; data.last_getting_gold = 0;
            return checkpoint.resumeRequested ? checkpoint.mapIndex : 0;
        }

        public static void SetCheckpoint(StatusSaveData data, string sceneName, int nextMapIndex, int totalMaps)
        {
            string key = SceneKey(sceneName);
            if (nextMapIndex <= 0 || totalMaps <= 0 || nextMapIndex > totalMaps) throw new ArgumentException("Invalid checkpoint index");
            if (data.active_run.sceneName != key || !TryGetActiveCheckpoint(data, out var checkpoint) || checkpoint.completed)
                Enter(data, key, totalMaps);
            CaptureActiveRun(data);
            checkpoint = data.checkpoint[key];
            checkpoint.mapIndex = nextMapIndex; checkpoint.totalMaps = totalMaps;
            checkpoint.resumeRequested = true;
            checkpoint.broken_traps_snapshot = data.broken_traps;
        }

        public static bool RestoreCheckpoint(StatusSaveData data)
        {
            CaptureActiveRun(data);
            bool resume = TryGetActiveCheckpoint(data, out var checkpoint) && !checkpoint.completed && checkpoint.resumeRequested;
            data.broken_traps = resume ? checkpoint.broken_traps_snapshot : 0;
            if (checkpoint != null && !checkpoint.completed) checkpoint.run_broken_traps = data.broken_traps;
            data.clear_reward_claimed = false; data.last_getting_gold = 0;
            return resume;
        }

        public static bool PrepareClear(StatusSaveData data, string sceneName, int totalMaps)
        {
            if (totalMaps <= 0 || data.active_run.sceneName != SceneKey(sceneName) ||
                !TryGetActiveCheckpoint(data, out var checkpoint) || checkpoint.completed) return false;
            CaptureActiveRun(data);
            checkpoint.totalMaps = totalMaps;
            data.active_run.pending_clear = true;
            return true;
        }

        public static bool CommitClear(StatusSaveData data)
        {
            if (!data.active_run.pending_clear || !TryGetActiveCheckpoint(data, out var checkpoint) ||
                checkpoint.completed || checkpoint.totalMaps <= 0) return false;
            CaptureActiveRun(data);
            var record = GetRecord(data, data.active_run.sceneName);
            if (record.last_committed_clear_run_id == data.active_run.run_id) return false;
            bool first = record.clear_count == 0;
            record.best_clear_time_seconds = first ? checkpoint.run_time_seconds : Math.Min(record.best_clear_time_seconds, checkpoint.run_time_seconds);
            record.best_hit_count = first ? checkpoint.run_hit_count : Math.Min(record.best_hit_count, checkpoint.run_hit_count);
            record.max_broken_traps = first ? checkpoint.run_broken_traps : Math.Max(record.max_broken_traps, checkpoint.run_broken_traps);
            if (record.clear_count < int.MaxValue) record.clear_count++;
            record.last_committed_clear_run_id = data.active_run.run_id;
            data.dungeon_records[data.active_run.sceneName] = record;
            checkpoint.mapIndex = checkpoint.totalMaps; checkpoint.resumeRequested = false; checkpoint.completed = true;
            data.active_run.pending_clear = false;
            return true;
        }

        public static bool HasCompletedActiveRun(StatusSaveData data) =>
            TryGetActiveCheckpoint(data, out var checkpoint) && checkpoint.completed &&
            GetRecord(data, data.active_run.sceneName).last_committed_clear_run_id == data.active_run.run_id;
    }
}
