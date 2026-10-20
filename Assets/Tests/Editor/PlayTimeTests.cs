using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Dungeon.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dungeon.Tests
{
    public class PlayTimeTests
    {
        private const string A = "DUNGEON_001", B = "DUNGEON_002";

        [TestCase("135:26", 8126)]
        [TestCase("25:01", 1501)]
        [TestCase("0:00", 0)]
        [TestCase("", 0)]
        public void Version3TimeMigratesAsHoursAndMinutes(string value, long minutes)
        {
            string json = "{\"save_version\":3,\"play_time\":\"" + value + "\"}";
            Assert.That(StatusSaveDefaults.TryDeserialize(json, out var data, out var error), Is.True, error);
            Assert.That(data.play_time_minutes, Is.EqualTo(minutes));
            Assert.That(data.play_time, Is.EqualTo(PlayTimeData.Format(minutes)));
            Assert.That(data.save_version, Is.EqualTo(StatusSaveDefaults.CurrentSaveVersion));
        }

        [TestCase("135:99")]
        [TestCase("unknown")]
        [TestCase("-1:00")]
        [TestCase("9999999999999999999:00")]
        public void UnparseableLegacyTimeIsPreservedWithoutGuessing(string value)
        {
            Assert.That(StatusSaveDefaults.TryDeserialize("{\"save_version\":3,\"play_time\":\"" + value + "\"}", out var data, out _), Is.True);
            Assert.That(data.play_time_minutes, Is.Zero);
            Assert.That(data.play_time_legacy_unparsed, Is.EqualTo(value));
            Assert.That(StatusSaveDefaults.TryDeserialize(StatusSaveDefaults.Serialize(data), out var reloaded, out _), Is.True);
            Assert.That(reloaded.play_time_legacy_unparsed, Is.EqualTo(value));
        }

        [Test]
        public void NumericMinutesAreAuthoritativeAndHoursDoNotWrap()
        {
            var data = StatusSaveDefaults.Create();
            data.play_time_minutes = 1501; data.play_time = "999:59";
            StatusSaveDefaults.Normalize(data);
            Assert.That(data.play_time, Is.EqualTo("25:01"));
        }

        [Test]
        public void MinuteBoundarySavesOnceAndDelayedFramesBatchMinutes()
        {
            var data = StatusSaveDefaults.Create(); var clock = new PlayTimeSession(data, 0, 0, "MainMenu");
            clock.Sample(data, 59.9, 59.9, "MainMenu");
            Assert.That(clock.IsSaveDue(59.9), Is.False);
            clock.Sample(data, 60, 60, "MainMenu");
            Assert.That(data.play_time_minutes, Is.EqualTo(1));
            Assert.That(clock.IsSaveDue(60), Is.True);
            clock.Saved(); Assert.That(clock.IsSaveDue(60), Is.False);
            clock.Sample(data, 245.5, 245.5, "Shop");
            Assert.That(data.play_time_minutes, Is.EqualTo(4));
            Assert.That(data.play_time_remainder_seconds, Is.EqualTo(5.5).Within(.00001));
            Assert.That(clock.IsSaveDue(245.5), Is.True);
            clock.Saved(); Assert.That(clock.IsSaveDue(245.5), Is.False);
        }

        [Test]
        public void ShortSessionsKeepRemainderAndDoNotCountOfflineGap()
        {
            var data = StatusSaveDefaults.Create();
            var first = new PlayTimeSession(data, 100, 0, "MainMenu");
            first.Sample(data, 120, 20, "MainMenu");
            Assert.That(StatusSaveDefaults.TryDeserialize(StatusSaveDefaults.Serialize(data), out data, out _), Is.True);
            var second = new PlayTimeSession(data, 10000, 0, "Guild");
            second.Sample(data, 10040.5, 40.5, "Guild");
            Assert.That(data.play_time_minutes, Is.EqualTo(1));
            Assert.That(data.play_time_remainder_seconds, Is.EqualTo(.5).Within(.00001));
        }

        [Test]
        public void TotalCountsPausedTimeWhileDungeonUsesScaledTime()
        {
            var data = StatusSaveDefaults.Create(); DungeonProgressService.Enter(data, A, 6);
            var clock = new PlayTimeSession(data, 0, 0, A);
            clock.Sample(data, 10, 10, A);
            clock.Sample(data, 70, 10, A);
            Assert.That(data.play_time_minutes, Is.EqualTo(1));
            Assert.That(data.clear_time_seconds, Is.EqualTo(10));
            clock.Sample(data, 75, 15, A);
            Assert.That(data.clear_time_seconds, Is.EqualTo(15));
        }

        [Test]
        public void CheckpointWaitResumeClearAndBestTimeKeepTheirOwnMeaning()
        {
            var data = StatusSaveDefaults.Create(); DungeonProgressService.Enter(data, A, 6);
            var clock = new PlayTimeSession(data, 0, 0, A);
            clock.Sample(data, 12.5, 12.5, A);
            DungeonProgressService.SetCheckpoint(data, A, 2, 6);
            clock.BindRun(data, "CheckpointScene");
            clock.Sample(data, 92.5, 92.5, "CheckpointScene");
            Assert.That(data.clear_time_seconds, Is.EqualTo(12));
            clock.BindRun(data, A); clock.Sample(data, 100, 100, A);
            Assert.That(data.clear_time_seconds, Is.EqualTo(20));
            DungeonProgressService.PrepareClear(data, A, 6); clock.BindRun(data, "ClearScene");
            Assert.That(DungeonProgressService.CommitClear(data), Is.True);
            clock.Sample(data, 130, 130, "ClearScene");
            Assert.That(data.clear_time_seconds, Is.EqualTo(20));
            Assert.That(data.dungeon_records[A].best_clear_time_seconds, Is.EqualTo(20));
            DungeonProgressService.Enter(data, A, 6); clock.BindRun(data, A);
            clock.Sample(data, 155, 155, A);
            DungeonProgressService.PrepareClear(data, A, 6); DungeonProgressService.CommitClear(data);
            Assert.That(data.clear_time_seconds, Is.EqualTo(25));
            Assert.That(data.dungeon_records[A].best_clear_time_seconds, Is.EqualTo(20));
        }

        [Test]
        public void SwitchingDungeonsPreservesIndependentSecondsAndDoesNotCountMenu()
        {
            var data = StatusSaveDefaults.Create(); DungeonProgressService.Enter(data, A, 6);
            var clock = new PlayTimeSession(data, 0, 0, A);
            clock.Sample(data, 8.25, 8.25, "WorldMap");
            DungeonProgressService.Enter(data, B, 4); clock.BindRun(data, "WorldMap");
            clock.Sample(data, 20, 20, B); clock.Sample(data, 25.5, 25.5, B);
            Assert.That(data.clear_time_seconds, Is.EqualTo(5));
            DungeonProgressService.Enter(data, A, 6); clock.BindRun(data, A);
            Assert.That(data.clear_time_seconds, Is.EqualTo(8));
            clock.Sample(data, 27, 27, A);
            Assert.That(data.clear_time_seconds, Is.EqualTo(9));
            Assert.That(data.checkpoint[B].run_time_seconds, Is.EqualTo(5));
        }

        [Test]
        public void FailedPeriodicSaveRetriesAfterDelayWithoutDoubleAddingMinutes()
        {
            var data = StatusSaveDefaults.Create(); var clock = new PlayTimeSession(data, 0, 0, "MainMenu");
            clock.Sample(data, 60, 60, "MainMenu"); clock.SaveFailed(60);
            Assert.That(clock.IsSaveDue(64.9), Is.False);
            clock.Sample(data, 65, 65, "MainMenu");
            Assert.That(clock.IsSaveDue(65), Is.True);
            Assert.That(data.play_time_minutes, Is.EqualTo(1));
            Assert.That(data.play_time_remainder_seconds, Is.EqualTo(5));
            clock.Saved(); Assert.That(clock.IsSaveDue(65), Is.False);
        }

        [Test]
        public void FailedCheckpointTransactionKeepsMeasuredTimeAndCanRetry()
        {
            var go = new GameObject("PlayTimeTransactionTest"); go.SetActive(false);
            try
            {
                var save = go.AddComponent<StatusSaveFile>(); var storage = new MemoryStorage();
                var data = StatusSaveDefaults.Create(); DungeonProgressService.Enter(data, A, 6);
                var clock = new PlayTimeSession(data, 0, 0, A);
                clock.Sample(data, 65.25, 65.25, A);
                Set(save, "data", data); Set(save, "storage", storage);
                // 이 테스트는 실제 Unity 시계 대신 위에서 측정한 데이터를 저장합니다.
                storage.fail = true;
                LogAssert.Expect(LogType.Error, new Regex("\\[StatusSaveFile\\] Save failed"));
                Assert.That(save.TryUpdateDungeonProgress(d => { DungeonProgressService.SetCheckpoint(d, A, 2, 6); return true; }), Is.False);
                Assert.That(save.Data.clear_time_seconds, Is.EqualTo(65));
                Assert.That(save.Data.play_time_minutes, Is.EqualTo(1));
                Assert.That(save.Data.checkpoint[A].mapIndex, Is.Zero);
                storage.fail = false;
                Assert.That(save.TryUpdateDungeonProgress(d => { DungeonProgressService.SetCheckpoint(d, A, 2, 6); return true; }), Is.True);
                Assert.That(storage.writes, Is.EqualTo(1));
                Assert.That(StatusSaveDefaults.TryDeserialize(storage.json, out var restored, out _), Is.True);
                Assert.That(restored.checkpoint[A].run_time_seconds, Is.EqualTo(65));
                Assert.That(restored.play_time_remainder_seconds, Is.EqualTo(5.25));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void LiveTimerSurvivesFailedClearTransactionAndSuccessfulRetry()
        {
            var go = new GameObject("LiveTimerRollbackTest"); go.SetActive(false);
            try
            {
                var data = StatusSaveDefaults.Create(); DungeonProgressService.Enter(data, A, 6);
                var clock = new PlayTimeSession(data, Time.realtimeSinceStartupAsDouble - 65.25, Time.timeAsDouble - 65.25, A);
                var save = go.AddComponent<StatusSaveFile>(); var storage = new MemoryStorage { fail = true };
                Set(save, "data", data); Set(save, "storage", storage); Set(save, "playTimeSession", clock); Set(save, "timingSceneName", A);
                save.CapturePlayTime(); DungeonProgressService.PrepareClear(data, A, 6);
                float measured = data.clear_time_seconds;
                LogAssert.Expect(LogType.Error, new Regex("\\[StatusSaveFile\\] Save failed"));
                Assert.That(save.TryFinalizeDungeonClear(), Is.False);
                Assert.That(save.Data.clear_time_seconds, Is.EqualTo(measured));
                Assert.That(save.Data.play_time_minutes, Is.EqualTo(1));
                Assert.That(save.Data.active_run.pending_clear, Is.True);
                storage.fail = false;
                Assert.That(save.TryFinalizeDungeonClear(), Is.True);
                Assert.That(save.Data.dungeon_records[A].best_clear_time_seconds, Is.EqualTo(measured));
                Assert.That(save.Data.play_time_minutes, Is.EqualTo(1));
                Assert.That(clock.IsSaveDue(Time.realtimeSinceStartupAsDouble + 10), Is.False);
                Assert.That(storage.writes, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void SmallFrameDeltasStillAccumulateDuringLongRuns()
        {
            var data = StatusSaveDefaults.Create(); DungeonProgressService.Enter(data, A, 6);
            data.clear_time_seconds = 100000;
            var clock = new PlayTimeSession(data, 0, 0, A);
            for (int i = 1; i <= 1000; i++) clock.Sample(data, i / 1000d, i / 1000d, A);
            Assert.That(data.clear_time_seconds, Is.EqualTo(100001).Within(.01f));
        }

        [Test]
        public void Version3TimeMigrationRetainsOriginalAtomicBackup()
        {
            string folder = Path.Combine(Path.GetTempPath(), "PlayTimeTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var go = new GameObject("TimeMigrationTest"); go.SetActive(false);
            try
            {
                var storage = new FileStatusSaveStorage(Path.Combine(folder, "active.json"), Path.Combine(folder, "default.json"), Path.Combine(folder, "backup.json"), Path.Combine(folder, "Recovery"));
                const string original = "{\"save_version\":3,\"play_time\":\"135:26\",\"main_stats\":{},\"sub_stats\":{},\"inventory\":{},\"equips\":{},\"checkpoint\":{}}";
                File.WriteAllText(storage.ActivePath, original);
                var save = go.AddComponent<StatusSaveFile>(); Set(save, "storage", storage); save.Load();
                Assert.That(save.Data.play_time_minutes, Is.EqualTo(8126));
                Assert.That(File.ReadAllText(storage.BackupPath), Is.EqualTo(original));
                Assert.That(StatusSaveDefaults.TryDeserialize(File.ReadAllText(storage.ActivePath), out var restored, out _), Is.True);
                Assert.That(restored.play_time_minutes, Is.EqualTo(8126));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); Directory.Delete(folder, true); }
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private sealed class MemoryStorage : IStatusSaveStorage
        {
            public bool fail; public int writes; public string json;
            public string ActivePath => "time-memory"; public string DefaultPath => "time-default";
            public string BackupPath => "time-backup"; public string TemporaryPath => "time-temp";
            public string LastError => "expected failure"; public string LastRecoveryAction => "";
            public bool TryLoadOrCreate(Func<string, bool> validator, string initial, out string value) { value = json ?? initial; return true; }
            public bool TryWrite(string value, Func<string, bool> validator) { if (fail || !validator(value)) return false; json = value; writes++; return true; }
        }
    }
}
