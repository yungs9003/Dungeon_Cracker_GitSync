using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Dungeon.Core
{
    public static class DungeonProgressMigration
    {
        public static bool IsLegacyJson(string json)
        {
            var root = JObject.Parse(json);
            return ((int?)root["save_version"] ?? 0) < StatusSaveDefaults.CurrentSaveVersion || root["checkpoint"]?["sceneName"] != null;
        }

        public static JObject Upgrade(JObject root)
        {
            if ((int?)root["save_version"] > StatusSaveDefaults.CurrentSaveVersion)
                throw new InvalidOperationException("현재 프로그램보다 새로운 저장 버전입니다.");
            if (root["checkpoint"] is JObject legacy && legacy["sceneName"] != null)
            {
                var checkpoints = new JObject();
                string scene = DungeonProgressService.SceneKey((string)legacy["sceneName"]);
                int index = (int?)legacy["mapIndex"] ?? -1;
                if (!string.IsNullOrWhiteSpace(scene) && index >= 0)
                {
                    string run = "legacy-" + scene;
                    checkpoints[scene] = new JObject
                    {
                        ["mapIndex"] = index, ["totalMaps"] = 0,
                        ["resumeRequested"] = (bool?)legacy["resumeRequested"] ?? false,
                        ["broken_traps_snapshot"] = (int?)legacy["broken_traps_snapshot"] ?? 0,
                        ["run_id"] = run,
                        ["run_time_seconds"] = (float?)root["clear_time_seconds"] ?? 0,
                        ["run_hit_count"] = (int?)root["hit_count"] ?? 0,
                        ["run_broken_traps"] = (int?)root["broken_traps"] ?? 0
                    };
                    root["active_run"] = new JObject { ["sceneName"] = scene, ["run_id"] = run, ["pending_clear"] = false };
                }
                root["checkpoint"] = checkpoints;
            }
            // Json.NET의 정수 변환 반올림 전에 구형 소수 시간을 명시적으로 버립니다.
            FloorTime(root, "clear_time_seconds");
            if (root["checkpoint"] is JObject checkpointMap)
                foreach (var pair in checkpointMap.Properties())
                    if (pair.Value is JObject checkpoint) FloorTime(checkpoint, "run_time_seconds");
            if (root["dungeon_records"] is JObject recordMap)
                foreach (var pair in recordMap.Properties())
                    if (pair.Value is JObject record) FloorTime(record, "best_clear_time_seconds", 3599);
            root["save_version"] = StatusSaveDefaults.CurrentSaveVersion;
            return root;
        }

        public static void Normalize(StatusSaveData data)
        {
            var records = new Dictionary<string, DungeonRecordData>();
            foreach (var pair in data.dungeon_records ?? new Dictionary<string, DungeonRecordData>())
            {
                string key = DungeonProgressService.SceneKey(pair.Key);
                if (string.IsNullOrEmpty(key)) continue;
                if (records.ContainsKey(key)) throw new InvalidOperationException("중복 던전 기록: " + key);
                var record = pair.Value ?? new DungeonRecordData();
                record.clear_count = Math.Max(0, record.clear_count);
                record.last_committed_clear_run_id ??= "";
                if (record.clear_count == 0)
                {
                    record.best_clear_time_seconds = 3599; record.best_hit_count = 399; record.max_broken_traps = 0;
                }
                else
                {
                    record.best_clear_time_seconds = Math.Max(0, record.best_clear_time_seconds);
                    record.best_hit_count = Math.Max(0, record.best_hit_count);
                    record.max_broken_traps = Math.Max(0, record.max_broken_traps);
                }
                records[key] = record;
            }
            var checkpoints = new Dictionary<string, DungeonCheckpointData>();
            foreach (var pair in data.checkpoint ?? new Dictionary<string, DungeonCheckpointData>())
            {
                string key = DungeonProgressService.SceneKey(pair.Key);
                if (string.IsNullOrEmpty(key)) continue;
                if (checkpoints.ContainsKey(key)) throw new InvalidOperationException("중복 체크포인트: " + key);
                var checkpoint = pair.Value ?? new DungeonCheckpointData();
                checkpoint.totalMaps = Math.Max(0, checkpoint.totalMaps);
                checkpoint.mapIndex = Math.Max(0, checkpoint.mapIndex);
                if (checkpoint.totalMaps > 0) checkpoint.mapIndex = Math.Min(checkpoint.mapIndex, checkpoint.totalMaps);
                if (checkpoint.completed) checkpoint.resumeRequested = false;
                else if (checkpoint.mapIndex > 0) checkpoint.resumeRequested = true;
                checkpoint.broken_traps_snapshot = Math.Max(0, checkpoint.broken_traps_snapshot);
                checkpoint.run_id ??= "";
                if (checkpoint.resumeRequested && checkpoint.run_id.Length == 0) checkpoint.run_id = "legacy-" + key;
                checkpoint.run_time_seconds = Math.Max(0, checkpoint.run_time_seconds);
                checkpoint.run_hit_count = Math.Max(0, checkpoint.run_hit_count);
                checkpoint.run_broken_traps = Math.Max(0, checkpoint.run_broken_traps);
                checkpoints[key] = checkpoint;
                if (!records.ContainsKey(key)) records[key] = new DungeonRecordData();
            }
            if (!records.ContainsKey("DUNGEON_001")) records["DUNGEON_001"] = new DungeonRecordData();
            if (!checkpoints.ContainsKey("DUNGEON_001")) checkpoints["DUNGEON_001"] = new DungeonCheckpointData();
            data.dungeon_records = records; data.checkpoint = checkpoints;
            data.active_run ??= new ActiveDungeonRunData();
            data.active_run.sceneName = DungeonProgressService.SceneKey(data.active_run.sceneName);
            data.active_run.run_id ??= "";
            data.clear_time_seconds = Math.Max(0, data.clear_time_seconds);
        }

        private static void FloorTime(JObject root, string field, int fallback = 0)
        {
            if (root[field] == null) return;
            root[field] = DungeonTime.WholeSeconds((double?)root[field] ?? fallback, fallback);
        }
    }
}
