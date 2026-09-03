using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Dungeon.Core
{
    public interface IStatusSaveStorage
    {
        string ActivePath { get; }
        string DefaultPath { get; }
        string BackupPath { get; }
        string TemporaryPath { get; }
        string LastError { get; }
        string LastRecoveryAction { get; }

        bool TryLoadOrCreate(
            Func<string, bool> validator,
            string generatedDefaultJson,
            out string json);

        bool TryWrite(string json, Func<string, bool> validator);
    }

    public static class StatusSaveStorageFactory
    {
        public const string DefaultFileName = "savefile.json";
        public const string EditorTestFileName = "savefile.test.json";
        public const string PlayerBackupFileName = "savefile.backup.json";

        public static IStatusSaveStorage Create()
        {
            string defaultPath = Path.Combine(
                Application.streamingAssetsPath,
                DefaultFileName);

#if UNITY_EDITOR
            string projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string backupDirectory = Path.Combine(
                projectRoot,
                "Library",
                "DungeonSaveBackups");

            return new FileStatusSaveStorage(
                Path.Combine(Application.streamingAssetsPath, EditorTestFileName),
                defaultPath,
                Path.Combine(backupDirectory, "savefile.test.backup.json"),
                Path.Combine(backupDirectory, "Recovery"));
#else
            return new FileStatusSaveStorage(
                Path.Combine(Application.persistentDataPath, DefaultFileName),
                defaultPath,
                Path.Combine(Application.persistentDataPath, PlayerBackupFileName),
                Path.Combine(Application.persistentDataPath, "SaveRecovery"));
#endif
        }
    }

    /// <summary>
    /// JSON 저장을 임시 파일 + flush + 검증 + 원자적 교체 순서로 수행합니다.
    /// 저장 도중 프로세스가 중단되어도 기존 정상본 또는 완성된 임시본으로 복구합니다.
    /// </summary>
    public sealed class FileStatusSaveStorage : IStatusSaveStorage
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new(false);

        public string ActivePath { get; }
        public string DefaultPath { get; }
        public string BackupPath { get; }
        public string TemporaryPath => ActivePath + ".tmp";
        public string LastError { get; private set; }
        public string LastRecoveryAction { get; private set; }

        private readonly string recoveryDirectory;

        public FileStatusSaveStorage(
            string activePath,
            string defaultPath,
            string backupPath,
            string recoveryDirectory)
        {
            ActivePath = activePath ?? throw new ArgumentNullException(nameof(activePath));
            DefaultPath = defaultPath ?? throw new ArgumentNullException(nameof(defaultPath));
            BackupPath = backupPath ?? throw new ArgumentNullException(nameof(backupPath));
            this.recoveryDirectory = recoveryDirectory ?? throw new ArgumentNullException(nameof(recoveryDirectory));
        }

        public bool TryLoadOrCreate(
            Func<string, bool> validator,
            string generatedDefaultJson,
            out string json)
        {
            json = string.Empty;
            LastError = string.Empty;
            LastRecoveryAction = string.Empty;

            if (validator == null)
            {
                LastError = "세이브 JSON validator가 없습니다.";
                return false;
            }

            try
            {
                bool activeExists = File.Exists(ActivePath);
                bool activeValid = TryReadValidFile(ActivePath, validator, out string activeJson);

                if (File.Exists(TemporaryPath))
                {
                    bool temporaryValid = TryReadValidFile(
                        TemporaryPath,
                        validator,
                        out string temporaryJson);
                    bool temporaryIsNewer = !activeExists ||
                        File.GetLastWriteTimeUtc(TemporaryPath) >= File.GetLastWriteTimeUtc(ActivePath);

                    if (temporaryValid && temporaryIsNewer)
                    {
                        if (!activeValid && activeExists)
                            PreserveFile(ActivePath, "invalid-active");

                        if (RepairActiveFile(temporaryJson, validator, activeValid))
                        {
                            json = temporaryJson;
                            LastRecoveryAction = "중단된 저장의 완성된 임시 파일을 복구했습니다.";
                            return true;
                        }
                    }

                    // 오래됐거나 불완전한 임시 파일은 진단용으로 보존한 뒤 제거합니다.
                    if (!temporaryValid)
                        PreserveFile(TemporaryPath, "invalid-temporary");

                    TryDeleteTemporaryFile();
                }

                if (activeValid)
                {
                    json = activeJson;
                    return true;
                }

                if (activeExists)
                    PreserveFile(ActivePath, "invalid-active");

                if (TryReadValidFile(BackupPath, validator, out string backupJson))
                {
                    if (RepairActiveFile(backupJson, validator, activeWasValid: false))
                    {
                        json = backupJson;
                        LastRecoveryAction = "활성 세이브가 손상되어 직전 정상 백업을 복구했습니다.";
                        return true;
                    }

                    // 활성 파일 복구 쓰기가 실패해도 검증된 백업으로 플레이는 계속할 수 있습니다.
                    json = backupJson;
                    LastRecoveryAction = "백업을 읽었지만 활성 세이브 복구 쓰기는 실패했습니다.";
                    return true;
                }

                string initialJson = string.Empty;
                string initialSource;

                if (TryReadValidFile(DefaultPath, validator, out string fileDefaultJson))
                {
                    initialJson = fileDefaultJson;
                    initialSource = "기본 savefile.json";
                }
                else if (validator(generatedDefaultJson))
                {
                    initialJson = generatedDefaultJson;
                    initialSource = "코드 기본값";
                }
                else
                {
                    LastError = "기본 파일과 코드 기본값이 모두 유효하지 않습니다.";
                    return false;
                }

                if (!RepairActiveFile(initialJson, validator, activeWasValid: false))
                    return false;

                json = initialJson;
                LastRecoveryAction = $"활성 세이브가 없어 {initialSource}에서 생성했습니다.";
                return true;
            }
            catch (Exception e)
            {
                LastError = $"세이브 로드/복구 실패: {e}";
                return false;
            }
        }

        public bool TryWrite(string json, Func<string, bool> validator)
        {
            LastError = string.Empty;

            if (validator == null)
            {
                LastError = "세이브 JSON validator가 없습니다.";
                return false;
            }

            if (!validator(json))
            {
                LastError = "유효하지 않은 JSON은 저장하지 않았습니다.";
                return false;
            }

            try
            {
                EnsureParentDirectory(ActivePath);
                EnsureParentDirectory(BackupPath);

                WriteTextDurably(TemporaryPath, json);

                if (!TryReadValidFile(TemporaryPath, validator, out _))
                {
                    LastError = "임시 파일 기록 후 검증에 실패했습니다. 기존 세이브는 유지됩니다.";
                    PreserveFile(TemporaryPath, "invalid-written-temporary");
                    return false;
                }

                if (File.Exists(ActivePath))
                {
                    File.Replace(
                        TemporaryPath,
                        ActivePath,
                        BackupPath,
                        ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(TemporaryPath, ActivePath);
                    File.Copy(ActivePath, BackupPath, overwrite: true);
                }

                if (!TryReadValidFile(ActivePath, validator, out _))
                {
                    LastError = "교체된 활성 세이브 검증에 실패했습니다. 다음 로드에서 백업 복구를 시도합니다.";
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                // 교체 전 예외라면 기존 활성 파일은 건드리지 않았고, 완성된 tmp는 다음 로드에서 복구합니다.
                LastError = $"세이브 안전 저장 실패: {e}";
                return false;
            }
        }

        private bool RepairActiveFile(
            string json,
            Func<string, bool> validator,
            bool activeWasValid)
        {
            try
            {
                if (!activeWasValid && File.Exists(ActivePath))
                    File.Delete(ActivePath);

                return TryWrite(json, validator);
            }
            catch (Exception e)
            {
                LastError = $"활성 세이브 복구 실패: {e}";
                return false;
            }
        }

        private static bool TryReadValidFile(
            string path,
            Func<string, bool> validator,
            out string json)
        {
            json = string.Empty;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            try
            {
                json = File.ReadAllText(path, Encoding.UTF8);
                return validator(json);
            }
            catch
            {
                json = string.Empty;
                return false;
            }
        }

        private static void WriteTextDurably(string path, string text)
        {
            EnsureParentDirectory(path);
            byte[] bytes = Utf8WithoutBom.GetBytes(text);

            using var stream = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough);

            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
        }

        private void PreserveFile(string sourcePath, string reason)
        {
            try
            {
                if (!File.Exists(sourcePath))
                    return;

                Directory.CreateDirectory(recoveryDirectory);
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
                string destination = Path.Combine(
                    recoveryDirectory,
                    $"{Path.GetFileName(sourcePath)}.{reason}.{timestamp}");

                File.Copy(sourcePath, destination, overwrite: false);
            }
            catch
            {
                // 보존 실패가 실제 세이브 복구를 막아서는 안 됩니다.
            }
        }

        private void TryDeleteTemporaryFile()
        {
            try
            {
                if (File.Exists(TemporaryPath))
                    File.Delete(TemporaryPath);
            }
            catch
            {
                // 다음 시작 때 다시 검사합니다.
            }
        }

        private static void EnsureParentDirectory(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
        }
    }
}
