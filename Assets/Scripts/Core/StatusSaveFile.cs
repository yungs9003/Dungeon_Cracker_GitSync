using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Dungeon.Core
{
    public class StatusSaveFile : MonoBehaviour
    {
        public static StatusSaveFile Instance { get; private set; }

        private static bool runtimeCopyPreparedThisPlaySession;

        [Header("Save File")]
        [SerializeField] private string sourceFileName = "savefile.json";

        [Header("Development Runtime Save")]
        [SerializeField] private bool useDevelopmentRuntimeCopy = true;
        [SerializeField] private string runtimeCopyFileName = "savefile_dev_runtime.json";
        [SerializeField] private bool deleteRuntimeCopyOnQuit = true;

        [Header("Debug")]
        [SerializeField] private bool logSavePath = true;

        private StatusSaveData data = new();
        private bool runtimeCopyDeleted;
        private bool isQuitting;

        public StatusSaveData Data => data;

        public event Action<StatusSaveData> OnSaveDataChanged;

        private string StreamingPath =>
            Path.Combine(Application.streamingAssetsPath, sourceFileName);

        private string NormalPersistentPath =>
            Path.Combine(Application.persistentDataPath, sourceFileName);

        private string RuntimePath =>
            Path.Combine(Application.persistentDataPath, runtimeCopyFileName);

        private string ActiveSavePath =>
            useDevelopmentRuntimeCopy ? RuntimePath : NormalPersistentPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateOnPlayStart()
        {
            Instance = null;
            runtimeCopyPreparedThisPlaySession = false;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (logSavePath)
                {
                    Debug.Log(
                        $"[StatusSaveFile] Duplicate destroyed. " +
                        $"ThisID={GetInstanceID()}, ExistingID={Instance.GetInstanceID()}"
                    );
                }

                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (transform.parent != null)
                transform.SetParent(null);

            DontDestroyOnLoad(gameObject);

            if (logSavePath)
            {
                Debug.Log(
                    $"[StatusSaveFile] Instance set. ID={GetInstanceID()}, " +
                    $"ActivePath={ActiveSavePath}"
                );
            }

            PrepareSaveFile();
            Load();
        }

        private void PrepareSaveFile()
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);

                if (useDevelopmentRuntimeCopy)
                    PrepareDevelopmentRuntimeSave();
                else
                    PrepareNormalPersistentSave();
            }
            catch (Exception e)
            {
                Debug.LogError($"[StatusSaveFile] PrepareSaveFile failed: {e}");
            }
        }

        private void PrepareDevelopmentRuntimeSave()
        {
            if (runtimeCopyPreparedThisPlaySession)
            {
                if (!File.Exists(RuntimePath))
                {
                    if (File.Exists(StreamingPath))
                    {
                        File.Copy(StreamingPath, RuntimePath, true);

                        if (logSavePath)
                        {
                            Debug.Log("[StatusSaveFile] Runtime save was missing, copied again from StreamingAssets.");
                            Debug.Log($"[StatusSaveFile] Source: {StreamingPath}");
                            Debug.Log($"[StatusSaveFile] Runtime: {RuntimePath}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[StatusSaveFile] StreamingAssets savefile missing: {StreamingPath}");
                        CreateDefaultSave(RuntimePath);
                    }
                }
                else if (logSavePath)
                {
                    Debug.Log($"[StatusSaveFile] Runtime save already prepared. Reusing: {RuntimePath}");
                }

                return;
            }

            runtimeCopyPreparedThisPlaySession = true;
            runtimeCopyDeleted = false;

            if (File.Exists(StreamingPath))
            {
                File.Copy(StreamingPath, RuntimePath, true);

                if (logSavePath)
                {
                    Debug.Log("[StatusSaveFile] Development runtime save copied for this Play session.");
                    Debug.Log($"[StatusSaveFile] Source: {StreamingPath}");
                    Debug.Log($"[StatusSaveFile] Runtime: {RuntimePath}");
                }

                return;
            }

            Debug.LogWarning($"[StatusSaveFile] StreamingAssets savefile missing: {StreamingPath}");
            CreateDefaultSave(RuntimePath);
        }

        private void PrepareNormalPersistentSave()
        {
            if (File.Exists(NormalPersistentPath))
            {
                if (logSavePath)
                    Debug.Log($"[StatusSaveFile] Persistent save exists: {NormalPersistentPath}");

                return;
            }

            if (File.Exists(StreamingPath))
            {
                File.Copy(StreamingPath, NormalPersistentPath, false);

                if (logSavePath)
                {
                    Debug.Log("[StatusSaveFile] Persistent save created from StreamingAssets.");
                    Debug.Log($"[StatusSaveFile] Save path: {NormalPersistentPath}");
                }

                return;
            }

            Debug.LogWarning($"[StatusSaveFile] StreamingAssets savefile missing: {StreamingPath}");
            CreateDefaultSave(NormalPersistentPath);
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(ActiveSavePath))
                    PrepareSaveFile();

                string json = File.ReadAllText(ActiveSavePath);
                data = JsonConvert.DeserializeObject<StatusSaveData>(json) ?? new StatusSaveData();

                EnsureNotNull();

                if (logSavePath)
                {
                    Debug.Log(
                        $"[StatusSaveFile] Loaded. ID={GetInstanceID()}, " +
                        $"Path={ActiveSavePath}, " +
                        $"user_name={data.user_name}, gold={data.gold}, " +
                        $"maxHP={data.main_stats.max_HP}, " +
                        $"broken_traps={data.broken_traps}, " +
                        $"clear_time={data.clear_time_seconds:F2}, " +
                        $"hit_count={data.hit_count}"
                    );
                }

                OnSaveDataChanged?.Invoke(data);
            }
            catch (Exception e)
            {
                Debug.LogError($"[StatusSaveFile] Load failed: {e}");

                data = new StatusSaveData();
                EnsureNotNull();
                OnSaveDataChanged?.Invoke(data);
            }
        }

        public void Save()
        {
            try
            {
                EnsureNotNull();

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(ActiveSavePath, json);

                if (logSavePath)
                {
                    Debug.Log(
                        $"[StatusSaveFile] Saved. ID={GetInstanceID()}, " +
                        $"Path={ActiveSavePath}, " +
                        $"gold={data.gold}, " +
                        $"maxHP={data.main_stats.max_HP}, " +
                        $"broken_traps={data.broken_traps}, " +
                        $"clear_time={data.clear_time_seconds:F2}, " +
                        $"hit_count={data.hit_count}"
                    );
                }

                OnSaveDataChanged?.Invoke(data);
            }
            catch (Exception e)
            {
                Debug.LogError($"[StatusSaveFile] Save failed: {e}");
            }
        }

        private void CreateDefaultSave(string path)
        {
            data = new StatusSaveData();
            EnsureNotNull();

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(path, json);

            if (logSavePath)
                Debug.Log($"[StatusSaveFile] Default save created: {path}");
        }

        private void EnsureNotNull()
        {
            data ??= new StatusSaveData();

            data.main_stats ??= new MainStats();
            data.sub_stats ??= new SubStats();
            data.checkpoint ??= new CheckpointData();

            data.item ??= new();
            data.equip ??= new();
            data.dungeon_status ??= new();
        }

        public void SetUserName(string userName)
        {
            EnsureNotNull();

            data.user_name = string.IsNullOrWhiteSpace(userName) ? "Toluse" : userName;
            Save();
        }

        public void SetGold(int gold)
        {
            EnsureNotNull();

            data.gold = Mathf.Max(0, gold);
            Save();
        }

        public void AddGold(int amount)
        {
            EnsureNotNull();

            data.gold = Mathf.Max(0, data.gold + amount);
            Save();
        }

        public void SetMainStats(int maxHp, int physicalResist, int magicalResist)
        {
            EnsureNotNull();

            data.main_stats.max_HP = Mathf.Max(1, maxHp);
            data.main_stats.physical_resist = physicalResist;
            data.main_stats.magical_resist = magicalResist;

            Save();
        }

        public void SetSubStats(
            int speed,
            int poisonResist,
            int electricResist,
            int amberResist,
            int freezeResist)
        {
            EnsureNotNull();

            data.sub_stats.speed = speed;
            data.sub_stats.poison_resist = poisonResist;
            data.sub_stats.electric_resist = electricResist;
            data.sub_stats.amber_resist = amberResist;
            data.sub_stats.freeze_resist = freezeResist;

            Save();
        }

        public int GetBrokenTraps()
        {
            EnsureNotNull();
            return Mathf.Max(0, data.broken_traps);
        }

        public void AddBrokenTrap(int amount = 1)
        {
            EnsureNotNull();

            if (amount <= 0)
                return;

            data.broken_traps += amount;

            data.clear_reward_claimed = false;
            data.last_getting_gold = 0;

            Save();

            Debug.Log($"[StatusSaveFile] broken_traps increased: {data.broken_traps}");
        }

        public void ResetBrokenTraps()
        {
            EnsureNotNull();

            data.broken_traps = 0;
            data.clear_reward_claimed = false;
            data.last_getting_gold = 0;

            Save();

            Debug.Log("[StatusSaveFile] broken_traps reset.");
        }

        public int GetClearRewardGold()
        {
            EnsureNotNull();

            int brokenTraps = Mathf.Max(0, data.broken_traps);
            return 500 + 50 * brokenTraps;
        }

        public int ClaimClearRewardGold()
        {
            EnsureNotNull();

            if (data.clear_reward_claimed)
            {
                Debug.Log(
                    $"[StatusSaveFile] Clear reward already claimed. " +
                    $"last_getting_gold={data.last_getting_gold}"
                );

                return data.last_getting_gold;
            }

            int rewardGold = GetClearRewardGold();

            data.gold = Mathf.Max(0, data.gold + rewardGold);
            data.last_getting_gold = rewardGold;
            data.clear_reward_claimed = true;

            Save();

            Debug.Log(
                $"[StatusSaveFile] Clear reward claimed. " +
                $"broken_traps={data.broken_traps}, reward={rewardGold}, totalGold={data.gold}"
            );

            return rewardGold;
        }

        public void ResetClearRewardState()
        {
            EnsureNotNull();

            data.clear_reward_claimed = false;
            data.last_getting_gold = 0;

            Save();
        }

        public float GetClearTimeSeconds()
        {
            EnsureNotNull();
            return Mathf.Max(0f, data.clear_time_seconds);
        }

        public int GetHitCount()
        {
            EnsureNotNull();
            return Mathf.Max(0, data.hit_count);
        }

        public void AddClearTime(float deltaSeconds, bool saveImmediately = false)
        {
            EnsureNotNull();

            if (deltaSeconds <= 0f)
                return;

            data.clear_time_seconds = Mathf.Max(0f, data.clear_time_seconds + deltaSeconds);

            if (saveImmediately)
                Save();
        }

        public void AddHitCount(int amount = 1)
        {
            EnsureNotNull();

            if (amount <= 0)
                return;

            data.hit_count += amount;

            Save();

            Debug.Log($"[StatusSaveFile] hit_count increased: {data.hit_count}");
        }

        public void ResetRunResultStats()
        {
            EnsureNotNull();

            data.broken_traps = 0;
            data.clear_reward_claimed = false;
            data.last_getting_gold = 0;
            data.clear_time_seconds = 0f;
            data.hit_count = 0;

            if (data.checkpoint != null)
            {
                data.checkpoint.sceneName = "";
                data.checkpoint.mapIndex = -1;
                data.checkpoint.resumeRequested = false;
                data.checkpoint.broken_traps_snapshot = 0;
            }

            Save();

            Debug.Log("[StatusSaveFile] Run result stats reset.");
        }

        public string GetActiveSavePath()
        {
            return ActiveSavePath;
        }

        public string GetSourceSavePath()
        {
            return StreamingPath;
        }

        private void OnApplicationQuit()
        {
            isQuitting = true;
            DeleteDevelopmentRuntimeCopy();
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            Instance = null;

            if (!isQuitting && logSavePath)
                Debug.Log($"[StatusSaveFile] Destroyed without deleting runtime save. ID={GetInstanceID()}");
        }

        private void DeleteDevelopmentRuntimeCopy()
        {
            if (runtimeCopyDeleted)
                return;

            if (!useDevelopmentRuntimeCopy)
                return;

            if (!deleteRuntimeCopyOnQuit)
                return;

            runtimeCopyDeleted = true;

            try
            {
                if (File.Exists(RuntimePath))
                {
                    File.Delete(RuntimePath);

                    if (logSavePath)
                        Debug.Log($"[StatusSaveFile] Deleted development runtime save: {RuntimePath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[StatusSaveFile] Failed to delete development runtime save: {e}");
            }
        }
    }
}