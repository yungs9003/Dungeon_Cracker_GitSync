using System;
using System.IO;
using System.Reflection;
using Dungeon.Core;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Tests
{
    public class DungeonClearRankTests
    {
        private static DungeonDefinition Definition(int hits = 20, int time = 120) => new()
        { sceneName = "DUNGEON_001", rank_reference_hit_count = hits, rank_reference_time_seconds = time };

        [TestCase(20, 120, 100, DungeonClearRank.S)]
        [TestCase(24, 132, 90, DungeonClearRank.S)]
        [TestCase(20, 150, 90, DungeonClearRank.S)]
        [TestCase(20, 195, 75, DungeonClearRank.A)]
        [TestCase(40, 180, 50, DungeonClearRank.B)]
        [TestCase(60, 240, 0, DungeonClearRank.C)]
        [TestCase(0, 0, 100, DungeonClearRank.S)]
        [TestCase(100, 1000, 0, DungeonClearRank.C)]
        [TestCase(20, 240, 60, DungeonClearRank.B)]
        [TestCase(60, 120, 40, DungeonClearRank.C)]
        public void ReferenceScoresAndInclusiveRankBoundaries(int hits, int time, int score, DungeonClearRank rank)
        {
            Assert.That(DungeonClearRankCalculator.TryCalculate(Definition(), hits, time, out var result), Is.True);
            Assert.That(result.TotalScore, Is.EqualTo(score));
            Assert.That(result.Rank, Is.EqualTo(rank));
        }

        [TestCase(20, 151, DungeonClearRank.A)]
        [TestCase(20, 196, DungeonClearRank.B)]
        [TestCase(40, 181, DungeonClearRank.C)]
        public void JustBelowRankThresholdDoesNotRoundUp(int hits, int time, DungeonClearRank rank)
        {
            DungeonClearRankCalculator.TryCalculate(Definition(), hits, time, out var result);
            Assert.That(result.Rank, Is.EqualTo(rank));
        }

        [Test]
        public void ScoreOf89Point96RemainsA()
        {
            DungeonClearRankCalculator.TryCalculate(Definition(100, 1000), 100, 1251, out var result);
            Assert.That(result.TotalScore, Is.EqualTo(89.96m));
            Assert.That(result.Rank, Is.EqualTo(DungeonClearRank.A));
        }

        [TestCase(0, 120)]
        [TestCase(-1, 120)]
        [TestCase(20, 0)]
        [TestCase(20, -1)]
        public void MissingOrInvalidReferencesDoNotInventARank(int hits, int time)
        {
            Assert.That(DungeonClearRankCalculator.TryCalculate(Definition(hits, time), 0, 0, out _), Is.False);
            Assert.That(DungeonClearRankCalculator.TryCalculate(null, 0, 0, out _), Is.False);
        }

        [Test]
        public void DatabaseProvidesFirstDungeonReferences()
        {
            Assert.That(GameDatabase.TryFindDungeonBySceneName("Dungeon_001", out var dungeon), Is.True);
            Assert.That(dungeon.rank_reference_hit_count, Is.EqualTo(20));
            Assert.That(dungeon.rank_reference_time_seconds, Is.EqualTo(120));
        }

        [TestCase(127.54, "02:07")]
        [TestCase(59.999, "00:59")]
        [TestCase(120.999, "02:00")]
        [TestCase(0.9, "00:00")]
        [TestCase(-1, "00:00")]
        public void TimeDisplayTruncatesToWholeSeconds(double seconds, string expected) =>
            Assert.That(DungeonTime.Format(seconds), Is.EqualTo(expected));

        private const string OldSave = @"{ ""save_version"":4, ""clear_time_seconds"":127.999,
            ""main_stats"":{}, ""sub_stats"":{}, ""inventory"":{}, ""equips"":{},
            ""checkpoint"":{ ""DUNGEON_001"":{ ""mapIndex"":2,""totalMaps"":5,""run_id"":""run"",""run_time_seconds"":127.999 } },
            ""active_run"":{ ""sceneName"":""DUNGEON_001"",""run_id"":""run"" },
            ""dungeon_records"":{ ""DUNGEON_001"":{ ""clear_count"":1,""best_clear_time_seconds"":119.999 } },
            ""play_time_minutes"":10, ""play_time_remainder_seconds"":9.75 }";

        [Test]
        public void OldFractionsAreFlooredAndAllDungeonTimeJsonValuesAreIntegers()
        {
            Assert.That(StatusSaveDefaults.TryDeserialize(OldSave, out var data, out var error), Is.True, error);
            Assert.That(data.clear_time_seconds, Is.EqualTo(127));
            Assert.That(data.checkpoint["DUNGEON_001"].run_time_seconds, Is.EqualTo(127));
            Assert.That(data.dungeon_records["DUNGEON_001"].best_clear_time_seconds, Is.EqualTo(119));
            Assert.That(data.play_time_remainder_seconds, Is.EqualTo(9.75));
            string json = StatusSaveDefaults.Serialize(data);
            StringAssert.Contains("\"clear_time_seconds\": 127,", json);
            StringAssert.Contains("\"run_time_seconds\": 127,", json);
            StringAssert.Contains("\"best_clear_time_seconds\": 119,", json);
            Assert.That(StatusSaveDefaults.TryDeserialize(json, out var reloaded, out _), Is.True);
            Assert.That(reloaded.clear_time_seconds, Is.EqualTo(127));
        }

        [Test]
        public void RepeatedSnapshotsDoNotDiscardSmallFrameTimeDuringSameRun()
        {
            var data = StatusSaveDefaults.Create(); DungeonProgressService.Enter(data, "DUNGEON_001", 5);
            var timer = new PlayTimeSession(data, 0, 0, "DUNGEON_001");
            timer.Sample(data, .6, .6, "DUNGEON_001");
            StatusSaveDefaults.Serialize(data); // 피격/구매 등 기존 저장
            Assert.That(data.clear_time_seconds, Is.Zero);
            timer.Sample(data, 1.2, 1.2, "DUNGEON_001");
            Assert.That(data.clear_time_seconds, Is.EqualTo(1));
            DungeonProgressService.SetCheckpoint(data, "DUNGEON_001", 2, 5);
            timer.BindRun(data, "CheckpointScene"); timer.Sample(data, 100, 100, "CheckpointScene");
            timer.BindRun(data, "DUNGEON_001"); timer.Sample(data, 100.9, 100.9, "DUNGEON_001");
            Assert.That(data.clear_time_seconds, Is.EqualTo(2));
        }

        [Test]
        public void WholeSecondMigrationPreservesPreviousFileInBackup()
        {
            string folder = Path.Combine(Path.GetTempPath(), "DungeonRankTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder); var go = new GameObject("WholeSecondMigration"); go.SetActive(false);
            try
            {
                var storage = new FileStatusSaveStorage(Path.Combine(folder, "active.json"), Path.Combine(folder, "default.json"), Path.Combine(folder, "backup.json"), Path.Combine(folder, "Recovery"));
                File.WriteAllText(storage.ActivePath, OldSave);
                var save = go.AddComponent<StatusSaveFile>();
                typeof(StatusSaveFile).GetField("storage", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(save, storage); save.Load();
                Assert.That(File.ReadAllText(storage.BackupPath), Is.EqualTo(OldSave));
                StringAssert.Contains("\"clear_time_seconds\": 127,", File.ReadAllText(storage.ActivePath));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); Directory.Delete(folder, true); }
        }
    }
}
