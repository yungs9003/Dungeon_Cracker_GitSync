using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Dungeon.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dungeon.Tests
{
    public sealed class DungeonAchievementTests
    {
        private const string A = "DUNGEON_001", B = "DUNGEON_002";
        private static DungeonDefinition Definition(string scene = A, int total = 41) => new()
        {
            sceneName = scene, rank_reference_hit_count = 20, rank_reference_time_seconds = 120,
            destructible_trap_count = total, achievement_reward_gold = 600
        };
        private static void Clear(StatusSaveData data, string scene = A, int time = 120, int hits = 20, int broken = 25)
        {
            DungeonProgressService.Enter(data, scene, 5);
            data.clear_time_seconds = time; data.hit_count = hits; data.broken_traps = broken;
            Assert.That(DungeonProgressService.PrepareClear(data, scene, 5), Is.True);
            Assert.That(DungeonProgressService.CommitClear(data), Is.True);
        }

        [Test]
        public void UnclearedDefaultValuesNeverUnlockChallenges()
        {
            var data = StatusSaveDefaults.Create();
            foreach (DungeonAchievementKind kind in Enum.GetValues(typeof(DungeonAchievementKind)))
                Assert.That(DungeonAchievementService.IsAchieved(data, Definition(total: 0), kind), Is.False);
            var result = DungeonAchievementService.Summarize(data, new[] { Definition() });
            Assert.That(result.ClearedDisplay, Is.EqualTo("0/1"));
            Assert.That(result.AchievementDisplay, Is.EqualTo("0/3"));
            Assert.That(data.dungeon_records[A].max_broken_traps, Is.Zero);
        }

        [TestCase(0, 0)] [TestCase(1, 1)] [TestCase(2, 2)] [TestCase(5, 3)]
        [TestCase(41, 25)] [TestCase(int.MaxValue, 1288490189)]
        public void SixtyPercentRoundsUpWithoutOverflow(int total, int required) =>
            Assert.That(DungeonAchievementService.RequiredDisarms(total), Is.EqualTo(required));

        [TestCase(0, "C")] [TestCase(74, "C")] [TestCase(75, "B")]
        [TestCase(149, "B")] [TestCase(150, "A")] [TestCase(224, "A")]
        [TestCase(225, "S")] [TestCase(300, "S")]
        public void AdventureRankUsesInclusiveQuarterBoundaries(int achieved, string expected) =>
            Assert.That(new AdventureRecordSummary(100, 0, achieved).AdventureRank, Is.EqualTo(expected));

        [Test]
        public void EmptyDungeonCatalogHasCRankWithoutDivisionByZero() =>
            Assert.That(new AdventureRecordSummary(0, 0, 0).AdventureRank, Is.EqualTo("C"));

        [Test]
        public void SavedAdventureRankUsesEarnedChallengesAndChangesWithDatabaseTotal()
        {
            var data = StatusSaveDefaults.Create(); data.player_rank = "S";
            var definition = Definition();
            DungeonAchievementService.RefreshSummary(data, new[] { definition });
            Assert.That(data.player_rank, Is.EqualTo("C"));
            Clear(data);
            DungeonAchievementService.RefreshSummary(data, new[] { definition });
            Assert.That(data.player_rank, Is.EqualTo("S"));
            Assert.That(data.guild_achievement_claims, Is.Empty);
            DungeonAchievementService.RefreshSummary(data, new[] { definition, Definition(B) });
            Assert.That(data.player_rank, Is.EqualTo("A"));
            Assert.That(StatusSaveDefaults.TryDeserialize(StatusSaveDefaults.Serialize(data), out var loaded, out _), Is.True);
            Assert.That(loaded.player_rank, Is.EqualTo("A"));
        }

        [TestCase(120, 20, 25, 3)] [TestCase(121, 20, 25, 2)]
        [TestCase(120, 21, 25, 2)] [TestCase(120, 20, 24, 2)] [TestCase(121, 21, 24, 0)]
        public void ThresholdsAreInclusiveAndUseAllThreeIndependentMetrics(int time, int hits, int broken, int expected)
        {
            var data = StatusSaveDefaults.Create(); Clear(data, time: time, hits: hits, broken: broken);
            Assert.That(DungeonAchievementService.Summarize(data, new[] { Definition() }).AchievedChallenges, Is.EqualTo(expected));
        }

        [Test]
        public void BestResultsFromDifferentClearsCombineAndDoNotRegress()
        {
            var data = StatusSaveDefaults.Create();
            Clear(data, time: 100, hits: 30, broken: 10);
            Clear(data, time: 150, hits: 10, broken: 25);
            Clear(data, time: 200, hits: 40, broken: 1);
            var result = DungeonAchievementService.Summarize(data, new[] { Definition() });
            Assert.That(result.AchievementDisplay, Is.EqualTo("3/3"));
            Assert.That(result.ClearedDisplay, Is.EqualTo("1/1"));
            Assert.That(data.dungeon_records[A].max_broken_traps, Is.EqualTo(25));
        }

        [Test]
        public void OnlyUniqueDatabaseDungeonsContributeToTotals()
        {
            var data = StatusSaveDefaults.Create(); Clear(data); Clear(data, "DUNGEON_099");
            var result = DungeonAchievementService.Summarize(data, new[] { Definition(), Definition("Dungeon_001"), Definition(B), null });
            Assert.That(result.ClearedDisplay, Is.EqualTo("1/2"));
            Assert.That(result.AchievementDisplay, Is.EqualTo("3/6"));
            Assert.That(DungeonAchievementService.Summarize(data, Array.Empty<DungeonDefinition>()).AchievementDisplay, Is.EqualTo("0/0"));
        }

        [Test]
        public void LiveDatabaseThresholdsAreUsedAndUncountedMetadataDoesNotPass()
        {
            var data = StatusSaveDefaults.Create(); Clear(data);
            var definition = Definition();
            definition.rank_reference_hit_count = 0; definition.rank_reference_time_seconds = 0; definition.destructible_trap_count = -1;
            Assert.That(DungeonAchievementService.Summarize(data, new[] { definition }).AchievedChallenges, Is.Zero);
            definition.destructible_trap_count = 0;
            Assert.That(DungeonAchievementService.IsAchieved(data, definition, DungeonAchievementKind.Disarm), Is.True);
        }

        [Test]
        public void OldSavesDefaultMissingBestDisarmsAndClaimsWithoutErasingOtherData()
        {
            var data = StatusSaveDefaults.Create(); Clear(data); data.gold = 987;
            string json = StatusSaveDefaults.Serialize(data).Replace("\"save_version\": " + StatusSaveDefaults.CurrentSaveVersion, "\"save_version\": 5");
            json = Regex.Replace(json, @"(?m)^\s*""guild_achievement_claims"": \{\},?\r?\n", "");
            json = Regex.Replace(json, @"(?m)^\s*""max_broken_traps"": \d+,\r?\n", "");
            Assert.That(StatusSaveDefaults.TryDeserialize(json, out var loaded, out _), Is.True);
            Assert.That(loaded.gold, Is.EqualTo(987));
            Assert.That(loaded.dungeon_records[A].max_broken_traps, Is.Zero);
            Assert.That(loaded.dungeon_records[A].best_hit_count, Is.EqualTo(20));
            Assert.That(loaded.guild_achievement_claims, Is.Empty);
            Assert.That(loaded.save_version, Is.EqualTo(StatusSaveDefaults.CurrentSaveVersion));
        }

        [Test]
        public void RewardClaimRequiresAchievementAndPaysOnceAcrossSerializationAndDungeons()
        {
            var data = StatusSaveDefaults.Create(); var definition = Definition();
            Assert.That(DungeonAchievementService.TryClaim(data, definition, DungeonAchievementService.HitsId), Is.False);
            Clear(data);
            Assert.That(DungeonAchievementService.TryClaim(data, definition, "unknown"), Is.False);
            Assert.That(DungeonAchievementService.TryClaim(data, definition, DungeonAchievementService.HitsId), Is.True);
            Assert.That(DungeonAchievementService.TryClaim(data, definition, DungeonAchievementService.HitsId), Is.False);
            Assert.That(data.gold, Is.EqualTo(600));
            Assert.That(StatusSaveDefaults.TryDeserialize(StatusSaveDefaults.Serialize(data), out data, out _), Is.True);
            Assert.That(DungeonAchievementService.TryClaim(data, definition, DungeonAchievementService.HitsId), Is.False);
            Assert.That(DungeonAchievementService.Summarize(data, new[] { definition }).AchievedChallenges, Is.EqualTo(3));
            Clear(data, B);
            Assert.That(DungeonAchievementService.TryClaim(data, Definition(B), DungeonAchievementService.HitsId), Is.True);
            Assert.That(data.gold, Is.EqualTo(1200));
        }

        [Test]
        public void GoldOverflowDoesNotConsumeRewardClaim()
        {
            var data = StatusSaveDefaults.Create(); Clear(data); data.gold = int.MaxValue;
            Assert.That(DungeonAchievementService.TryClaim(data, Definition(), DungeonAchievementService.HitsId), Is.False);
            Assert.That(data.guild_achievement_claims, Is.Empty);
        }

        [Test]
        public void FailedRewardWriteRollsBackGoldAndClaimAndCanBeRetriedOnce()
        {
            var data = StatusSaveDefaults.Create(); Clear(data);
            var go = new GameObject("AtomicAchievementTest"); go.SetActive(false);
            try
            {
                var save = go.AddComponent<StatusSaveFile>(); var storage = new MemoryStorage { fail = true };
                Set(save, "data", data); Set(save, "storage", storage);
                LogAssert.Expect(LogType.Error, new Regex("\\[StatusSaveFile\\] Save failed"));
                Assert.That(save.TryClaimDungeonAchievement(A, DungeonAchievementService.HitsId), Is.False);
                Assert.That(save.Data.gold, Is.Zero);
                Assert.That(save.Data.guild_achievement_claims, Is.Empty);
                storage.fail = false;
                Assert.That(save.TryClaimDungeonAchievement(A, DungeonAchievementService.HitsId), Is.True);
                Assert.That(save.TryClaimDungeonAchievement(A, DungeonAchievementService.HitsId), Is.False);
                Assert.That(storage.writes, Is.EqualTo(1));
                Assert.That(StatusSaveDefaults.TryDeserialize(storage.json, out var loaded, out _), Is.True);
                Assert.That(loaded.gold, Is.EqualTo(600));
                Assert.That(DungeonAchievementService.IsClaimed(loaded, A, DungeonAchievementService.HitsId), Is.True);
                Assert.That(loaded.clear_num_info, Is.EqualTo("1/1"));
                Assert.That(loaded.achievement, Is.EqualTo("3/3"));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private sealed class MemoryStorage : IStatusSaveStorage
        {
            public bool fail; public int writes; public string json;
            public string ActivePath => "memory"; public string DefaultPath => "memory-default";
            public string BackupPath => "memory-backup"; public string TemporaryPath => "memory-temp";
            public string LastError => "expected failure"; public string LastRecoveryAction => "";
            public bool TryLoadOrCreate(Func<string, bool> validator, string initial, out string result) { result = json ?? initial; return true; }
            public bool TryWrite(string value, Func<string, bool> validator) { if (fail) return false; json = value; writes++; return validator(value); }
        }
    }
}
