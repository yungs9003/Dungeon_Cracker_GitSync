using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Dungeon.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dungeon.Tests
{
    public class DungeonProgressTests
    {
        private const string A = "DUNGEON_001", B = "DUNGEON_002";
        private static StatusSaveData NewData() => StatusSaveDefaults.Create();

        [Test]
        public void DungeonMetadataAndMapListResolveWithoutWorldmapDatabaseObject()
        {
            Assert.That(GameDatabase.TryFindDungeonBySceneName("Dungeon_001", out var definition), Is.True);
            Assert.That(definition.number, Is.EqualTo(1));
            Assert.That(definition.name, Is.EqualTo("버려진 채굴장"));
            Assert.That(DungeonMapCatalog.Load("Dungeon_001").Length, Is.EqualTo(5));
        }
        private static void Complete(StatusSaveData data, float time, int hits, int broken)
        {
            DungeonProgressService.Enter(data, A, 6);
            data.clear_time_seconds = DungeonTime.WholeSeconds(time); data.hit_count = hits; data.broken_traps = broken;
            Assert.That(DungeonProgressService.PrepareClear(data, A, 6), Is.True);
            Assert.That(DungeonProgressService.CommitClear(data), Is.True);
        }

        [Test]
        public void UnclearedRecordsKeepSentinelsAndCannotSatisfyClearedRecordQuery()
        {
            var data = NewData();
            var record = data.dungeon_records[A];
            Assert.That(record.clear_count, Is.Zero);
            Assert.That(record.best_clear_time_seconds, Is.EqualTo(3599));
            Assert.That(record.best_hit_count, Is.EqualTo(399));
            Assert.That(DungeonProgressService.TryGetClearedRecord(data, A, out _), Is.False);
            record.best_clear_time_seconds = 0; record.best_hit_count = 0;
            StatusSaveDefaults.Normalize(data);
            Assert.That(record.best_clear_time_seconds, Is.EqualTo(3599));
            Assert.That(record.best_hit_count, Is.EqualTo(399));
        }

        [TestCase(4200f, 450, 7)]
        [TestCase(120f, 0, 0)]
        public void FirstActualClearStoresActualValuesEvenWhenWorseThanSentinels(float time, int hits, int broken)
        {
            var data = NewData(); Complete(data, time, hits, broken);
            var record = data.dungeon_records[A];
            Assert.That(record.clear_count, Is.EqualTo(1));
            Assert.That(record.best_clear_time_seconds, Is.EqualTo(time));
            Assert.That(record.best_hit_count, Is.EqualTo(hits));
            Assert.That(record.max_broken_traps, Is.EqualTo(broken));
            Assert.That(DungeonProgressService.TryGetClearedRecord(data, A, out _), Is.True);
        }

        [Test]
        public void RepeatedClearsCompareIndependentBestValues()
        {
            var data = NewData(); Complete(data, 4200, 450, 7); Complete(data, 5000, 400, 5); Complete(data, 100, 440, 9);
            var record = data.dungeon_records[A];
            Assert.That(record.clear_count, Is.EqualTo(3));
            Assert.That(record.best_clear_time_seconds, Is.EqualTo(100));
            Assert.That(record.best_hit_count, Is.EqualTo(400));
            Assert.That(record.max_broken_traps, Is.EqualTo(9));
        }

        [Test]
        public void ClearRequiresMatchingPendingRunAndCommitsOnlyOnce()
        {
            var data = NewData();
            Assert.That(DungeonProgressService.CommitClear(data), Is.False);
            DungeonProgressService.Enter(data, A, 6);
            Assert.That(DungeonProgressService.PrepareClear(data, B, 4), Is.False);
            Assert.That(DungeonProgressService.CommitClear(data), Is.False);
            Assert.That(DungeonProgressService.PrepareClear(data, A, 6), Is.True);
            Assert.That(DungeonProgressService.CommitClear(data), Is.True);
            Assert.That(DungeonProgressService.CommitClear(data), Is.False);
            Assert.That(data.dungeon_records[A].clear_count, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(2)]
        public void CheckpointEntryResumesRegardlessOfPreviousClearCount(int previousClears)
        {
            var data = NewData(); data.dungeon_records[A].clear_count = previousClears;
            DungeonProgressService.Enter(data, A, 6);
            data.clear_time_seconds = 90; data.hit_count = 4; data.broken_traps = 3;
            DungeonProgressService.SetCheckpoint(data, A, 2, 6);
            string run = data.active_run.run_id;
            Assert.That(DungeonProgressService.Enter(data, A, 6), Is.EqualTo(2));
            Assert.That(data.active_run.run_id, Is.EqualTo(run));
            Assert.That(data.checkpoint[A].mapIndex, Is.EqualTo(2));
            Assert.That(data.hit_count, Is.EqualTo(4));
            Assert.That(data.broken_traps, Is.EqualTo(3));
        }

        [Test]
        public void UncommittedMapsDoNotAdvanceProgressOrEraseRunOnEntry()
        {
            var data = NewData(); DungeonProgressService.Enter(data, A, 6);
            data.clear_time_seconds = 20; data.hit_count = 3;
            string run = data.active_run.run_id;
            Assert.That(DungeonProgressService.Enter(data, A, 6), Is.Zero);
            Assert.That(data.active_run.run_id, Is.EqualTo(run));
            Assert.That(data.checkpoint[A].mapIndex, Is.Zero);
            Assert.That(data.clear_time_seconds, Is.EqualTo(20));
        }

        [Test]
        public void CompletedNOverNAndLastCheckpointHaveDifferentEntryBehavior()
        {
            var data = NewData(); Complete(data, 40, 1, 2);
            string completed = data.active_run.run_id;
            Assert.That(data.checkpoint[A].mapIndex, Is.EqualTo(6));
            Assert.That(data.checkpoint[A].resumeRequested, Is.False);
            Assert.That(DungeonProgressService.Enter(data, A, 6), Is.Zero);
            Assert.That(data.active_run.run_id, Is.Not.EqualTo(completed));
            Assert.That(data.dungeon_records[A].clear_count, Is.EqualTo(1));
            Assert.That(DungeonProgressService.CommitClear(data), Is.False);
            DungeonProgressService.SetCheckpoint(data, A, 6, 6);
            Assert.That(DungeonProgressService.Enter(data, A, 6), Is.EqualTo(6));
            Assert.That(data.checkpoint[A].resumeRequested, Is.True);
        }

        [Test]
        public void SwitchingDungeonsKeepsEachCheckpointAndRunCounters()
        {
            var data = NewData(); DungeonProgressService.Enter(data, A, 6);
            data.clear_time_seconds = 30; data.hit_count = 2; data.broken_traps = 4;
            DungeonProgressService.SetCheckpoint(data, A, 2, 6);
            string aRun = data.active_run.run_id;
            DungeonProgressService.Enter(data, B, 4);
            Assert.That(data.hit_count, Is.Zero);
            data.clear_time_seconds = 70; data.hit_count = 8; data.broken_traps = 1;
            DungeonProgressService.SetCheckpoint(data, B, 1, 4);
            Assert.That(DungeonProgressService.Enter(data, A, 6), Is.EqualTo(2));
            Assert.That(data.active_run.run_id, Is.EqualTo(aRun));
            Assert.That(data.clear_time_seconds, Is.EqualTo(30));
            Assert.That(data.hit_count, Is.EqualTo(2));
            Assert.That(data.broken_traps, Is.EqualTo(4));
            Assert.That(data.checkpoint[B].mapIndex, Is.EqualTo(1));
            Assert.That(data.checkpoint[B].run_hit_count, Is.EqualTo(8));
        }

        [Test]
        public void DeathRestoresDisarmSnapshotAndKeepsTimeAndHits()
        {
            var data = NewData(); DungeonProgressService.Enter(data, A, 6);
            data.broken_traps = 2; DungeonProgressService.SetCheckpoint(data, A, 2, 6);
            data.broken_traps = 7; data.hit_count = 9; data.clear_time_seconds = 150;
            Assert.That(DungeonProgressService.RestoreCheckpoint(data), Is.True);
            Assert.That(data.broken_traps, Is.EqualTo(2));
            Assert.That(data.hit_count, Is.EqualTo(9));
            Assert.That(data.clear_time_seconds, Is.EqualTo(150));
        }

        private static string LegacyJson()
        {
            return @"{ ""save_version"":2, ""gold"":1234, ""clear_time_seconds"":45, ""hit_count"":7, ""broken_traps"":5,
                ""main_stats"":{}, ""sub_stats"":{},
                ""inventory"":{ ""item"":[{ ""000"":15 }], ""equipment"":[{ ""003"":1 }], ""module"":[{ ""001"":1 }] },
                ""equips"":{ ""item"":[{ ""slot1"":""000"" }], ""equip"":[{ ""head"":""003"" }] },
                ""checkpoint"":{ ""sceneName"":""Dungeon_001"", ""mapIndex"":2, ""resumeRequested"":true, ""broken_traps_snapshot"":3 },
                ""dungeon_status"":[{ ""001"":""clear:clear:clear:clear"" }] }";
        }

        [Test]
        public void Version2MigrationPreservesCheckpointAndInventoryWithoutInventingClearHistory()
        {
            string json = LegacyJson();
            Assert.That(StatusSaveDefaults.IsValidJson(json), Is.True);
            Assert.That(StatusSaveDefaults.TryDeserialize(json, out var data, out var error), Is.True, error);
            Assert.That(data.save_version, Is.EqualTo(StatusSaveDefaults.CurrentSaveVersion));
            Assert.That(data.gold, Is.EqualTo(1234));
            Assert.That(data.inventory.item[0]["000"], Is.EqualTo(15));
            Assert.That(data.inventory.equipment[0]["003"], Is.EqualTo(1));
            Assert.That(data.equips.item[0]["slot1"], Is.EqualTo("000"));
            Assert.That(data.equips.equip[0]["head"], Is.EqualTo("003"));
            Assert.That(data.checkpoint[A].mapIndex, Is.EqualTo(2));
            Assert.That(data.active_run.sceneName, Is.EqualTo(A));
            Assert.That(data.checkpoint[A].run_hit_count, Is.EqualTo(7));
            Assert.That(DungeonProgressService.Enter(data, A, 6), Is.EqualTo(2));
            Assert.That(data.broken_traps, Is.EqualTo(3));
            Assert.That(data.dungeon_records[A].clear_count, Is.Zero);
            Assert.That(data.dungeon_records[A].best_hit_count, Is.EqualTo(399));
            Assert.That(data.dungeon_status[0]["001"], Is.EqualTo("clear:clear:clear:clear"));
            Assert.That(StatusSaveDefaults.IsValidJson(StatusSaveDefaults.Serialize(data)), Is.True);
        }

        [Test]
        public void FutureVersionIsRejectedByDeserializer()
        {
            string json = StatusSaveDefaults.Serialize(NewData()).Replace("\"save_version\": " + StatusSaveDefaults.CurrentSaveVersion, "\"save_version\": 999");
            Assert.That(StatusSaveDefaults.IsValidJson(json), Is.False);
        }

        [Test]
        public void LoadMigrationKeepsOriginalFileInAtomicBackup()
        {
            string folder = Path.Combine(Path.GetTempPath(), "DungeonRecordsTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var storage = new FileStatusSaveStorage(Path.Combine(folder,"active.json"),Path.Combine(folder,"default.json"),Path.Combine(folder,"backup.json"),Path.Combine(folder,"Recovery"));
            string original = LegacyJson(); File.WriteAllText(storage.ActivePath,original);
            var go = new GameObject("MigrationTest"); go.SetActive(false);
            try
            {
                var save = go.AddComponent<StatusSaveFile>(); Set(save,"storage",storage); save.Load();
                StringAssert.Contains("\"save_version\": " + StatusSaveDefaults.CurrentSaveVersion, File.ReadAllText(storage.ActivePath));
                Assert.That(File.ReadAllText(storage.BackupPath), Is.EqualTo(original));
                Assert.That(save.Data.checkpoint[A].mapIndex, Is.EqualTo(2));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); Directory.Delete(folder,true); }
        }

        [Test]
        public void FailedCommitRollsBackMemoryAndRetryStillCommitsOnlyOnce()
        {
            var data=NewData(); DungeonProgressService.Enter(data,A,6);
            data.clear_time_seconds=4000;data.hit_count=410;data.broken_traps=3;DungeonProgressService.PrepareClear(data,A,6);
            var go=new GameObject("AtomicRecordTest");go.SetActive(false);
            try
            {
                var save=go.AddComponent<StatusSaveFile>();var storage=new MemoryStorage { fail=true };Set(save,"data",data);Set(save,"storage",storage);
                LogAssert.Expect(LogType.Error,new Regex("\\[StatusSaveFile\\] Save failed"));
                Assert.That(save.TryFinalizeDungeonClear(),Is.False);
                Assert.That(save.Data.dungeon_records[A].clear_count,Is.Zero);
                Assert.That(save.Data.active_run.pending_clear,Is.True);
                storage.fail=false;Assert.That(save.TryFinalizeDungeonClear(),Is.True);
                Assert.That(save.Data.dungeon_records[A].clear_count,Is.EqualTo(1));
                Assert.That(save.TryFinalizeDungeonClear(),Is.False);
                Assert.That(storage.writes,Is.EqualTo(1));
                Assert.That(StatusSaveDefaults.TryDeserialize(storage.json,out var restored,out _),Is.True);
                Assert.That(restored.dungeon_records[A].best_hit_count,Is.EqualTo(410));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static void Set(object target,string field,object value) => target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        private sealed class MemoryStorage:IStatusSaveStorage
        {
            public bool fail;public int writes;public string json;
            public string ActivePath=>"memory";public string DefaultPath=>"memory-default";public string BackupPath=>"memory-backup";public string TemporaryPath=>"memory-temp";
            public string LastError=>"expected failure";public string LastRecoveryAction=>"";
            public bool TryLoadOrCreate(Func<string,bool> validator,string initial,out string result){result=json??initial;return true;}
            public bool TryWrite(string value,Func<string,bool> validator){if(fail)return false;json=value;writes++;return validator(value);}
        }
    }
}
