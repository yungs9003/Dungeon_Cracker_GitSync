using System;
using System.IO;
using Dungeon.Core;
using NUnit.Framework;

namespace Dungeon.Tests
{
    public class StatusSaveStorageTests
    {
        private string testRoot;
        private FileStatusSaveStorage storage;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(
                Path.GetTempPath(),
                "DungeonSaveStorageTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);

            storage = new FileStatusSaveStorage(
                Path.Combine(testRoot, "savefile.test.json"),
                Path.Combine(testRoot, "savefile.json"),
                Path.Combine(testRoot, "savefile.backup.json"),
                Path.Combine(testRoot, "Recovery"));

            File.WriteAllText(
                storage.DefaultPath,
                StatusSaveDefaults.Serialize(StatusSaveDefaults.Create()));
        }

        [TearDown]
        public void TearDown()
        {
            if (string.IsNullOrWhiteSpace(testRoot) || !Directory.Exists(testRoot))
                return;

            string allowedRoot = Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "DungeonSaveStorageTests"));
            string resolvedTestRoot = Path.GetFullPath(testRoot);

            if (!resolvedTestRoot.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Unexpected test cleanup path: {resolvedTestRoot}");

            Directory.Delete(resolvedTestRoot, recursive: true);
        }

        [Test]
        public void TryWrite_ReplacesAtomicallyAndKeepsPreviousValidBackup()
        {
            string firstJson = CreateJsonWithGold(100);
            string secondJson = CreateJsonWithGold(250);

            Assert.That(storage.TryWrite(firstJson, StatusSaveDefaults.IsValidJson), Is.True, storage.LastError);
            Assert.That(storage.TryWrite(secondJson, StatusSaveDefaults.IsValidJson), Is.True, storage.LastError);

            Assert.That(ReadGold(storage.ActivePath), Is.EqualTo(250));
            Assert.That(ReadGold(storage.BackupPath), Is.EqualTo(100));
            Assert.That(File.Exists(storage.TemporaryPath), Is.False);
        }

        [Test]
        public void TryLoadOrCreate_PromotesNewerCompletedTemporaryFile()
        {
            Assert.That(
                storage.TryWrite(CreateJsonWithGold(100), StatusSaveDefaults.IsValidJson),
                Is.True,
                storage.LastError);

            File.WriteAllText(storage.TemporaryPath, CreateJsonWithGold(300));
            File.SetLastWriteTimeUtc(storage.TemporaryPath, DateTime.UtcNow.AddMinutes(1));

            Assert.That(
                storage.TryLoadOrCreate(
                    StatusSaveDefaults.IsValidJson,
                    StatusSaveDefaults.Serialize(StatusSaveDefaults.Create()),
                    out string loadedJson),
                Is.True,
                storage.LastError);

            Assert.That(ReadGoldFromJson(loadedJson), Is.EqualTo(300));
            Assert.That(ReadGold(storage.ActivePath), Is.EqualTo(300));
            Assert.That(File.Exists(storage.TemporaryPath), Is.False);
            Assert.That(storage.LastRecoveryAction, Does.Contain("임시 파일"));
        }

        [Test]
        public void TryLoadOrCreate_RecoversPreviousBackupWhenActiveFileIsCorrupt()
        {
            Assert.That(
                storage.TryWrite(CreateJsonWithGold(100), StatusSaveDefaults.IsValidJson),
                Is.True,
                storage.LastError);
            Assert.That(
                storage.TryWrite(CreateJsonWithGold(200), StatusSaveDefaults.IsValidJson),
                Is.True,
                storage.LastError);

            File.WriteAllText(storage.ActivePath, "{}");

            Assert.That(
                storage.TryLoadOrCreate(
                    StatusSaveDefaults.IsValidJson,
                    StatusSaveDefaults.Serialize(StatusSaveDefaults.Create()),
                    out string loadedJson),
                Is.True,
                storage.LastError);

            Assert.That(ReadGoldFromJson(loadedJson), Is.EqualTo(100));
            Assert.That(ReadGold(storage.ActivePath), Is.EqualTo(100));
            Assert.That(storage.LastRecoveryAction, Does.Contain("백업"));
            Assert.That(Directory.GetFiles(Path.Combine(testRoot, "Recovery")).Length, Is.GreaterThan(0));
        }

        [Test]
        public void TryLoadOrCreate_IgnoresAndPreservesInvalidTemporaryFile()
        {
            Assert.That(
                storage.TryWrite(CreateJsonWithGold(100), StatusSaveDefaults.IsValidJson),
                Is.True,
                storage.LastError);

            File.WriteAllText(storage.TemporaryPath, "{\"save_version\": 1}");
            File.SetLastWriteTimeUtc(storage.TemporaryPath, DateTime.UtcNow.AddMinutes(1));

            Assert.That(
                storage.TryLoadOrCreate(
                    StatusSaveDefaults.IsValidJson,
                    StatusSaveDefaults.Serialize(StatusSaveDefaults.Create()),
                    out string loadedJson),
                Is.True,
                storage.LastError);

            Assert.That(ReadGoldFromJson(loadedJson), Is.EqualTo(100));
            Assert.That(File.Exists(storage.TemporaryPath), Is.False);
            Assert.That(Directory.GetFiles(Path.Combine(testRoot, "Recovery")).Length, Is.GreaterThan(0));
        }

        [Test]
        public void ProjectDefaultFile_MatchesCodeDefaults()
        {
            string path = Path.GetFullPath("Assets/StreamingAssets/savefile.json");
            string json = File.ReadAllText(path);

            Assert.That(
                StatusSaveDefaults.TryDeserialize(json, out StatusSaveData data, out string error),
                Is.True,
                error);

            Assert.That(
                StatusSaveDefaults.Serialize(data),
                Is.EqualTo(StatusSaveDefaults.Serialize(StatusSaveDefaults.Create())));
        }

        [Test]
        public void EditorFactory_UsesTestFileAndKeepsDefaultReadOnly()
        {
            IStatusSaveStorage editorStorage = StatusSaveStorageFactory.Create();

            Assert.That(Path.GetFileName(editorStorage.ActivePath), Is.EqualTo("savefile.test.json"));
            Assert.That(Path.GetFileName(editorStorage.DefaultPath), Is.EqualTo("savefile.json"));
            Assert.That(editorStorage.ActivePath, Is.Not.EqualTo(editorStorage.DefaultPath));
        }

        private static string CreateJsonWithGold(int gold)
        {
            StatusSaveData data = StatusSaveDefaults.Create();
            data.gold = gold;
            return StatusSaveDefaults.Serialize(data);
        }

        private static int ReadGold(string path)
        {
            return ReadGoldFromJson(File.ReadAllText(path));
        }

        private static int ReadGoldFromJson(string json)
        {
            Assert.That(
                StatusSaveDefaults.TryDeserialize(json, out StatusSaveData data, out string error),
                Is.True,
                error);
            return data.gold;
        }
    }
}
