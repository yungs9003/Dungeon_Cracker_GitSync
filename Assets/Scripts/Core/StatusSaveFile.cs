using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Dungeon.Core
{
    public class StatusSaveFile : MonoBehaviour
    {
        public static StatusSaveFile Instance { get; private set; }

        [Header("Save File")]
        [SerializeField] private string fileName = "savefile.json";

        private StatusSaveData data = new();

        public StatusSaveData Data => data;

        public event Action<StatusSaveData> OnSaveDataChanged;

        private string PersistentPath => Path.Combine(Application.persistentDataPath, fileName);
        private string StreamingPath => Path.Combine(Application.streamingAssetsPath, fileName);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Load();
        }

        public void Load()
        {
            try
            {
                EnsureSaveFileExists();

                string json = File.ReadAllText(PersistentPath);
                data = JsonConvert.DeserializeObject<StatusSaveData>(json) ?? new StatusSaveData();

                EnsureNotNull();
                OnSaveDataChanged?.Invoke(data);
            }
            catch (Exception e)
            {
                Debug.LogError($"[StatusSaveFile] Load failed: {e}");
                data = new StatusSaveData();
                OnSaveDataChanged?.Invoke(data);
            }
        }

        public void Save()
        {
            try
            {
                EnsureNotNull();

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(PersistentPath, json);

                OnSaveDataChanged?.Invoke(data);
            }
            catch (Exception e)
            {
                Debug.LogError($"[StatusSaveFile] Save failed: {e}");
            }
        }

        private void EnsureSaveFileExists()
        {
            if (File.Exists(PersistentPath))
                return;

            if (File.Exists(StreamingPath))
            {
                File.Copy(StreamingPath, PersistentPath);
                return;
            }

            data = new StatusSaveData();
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(PersistentPath, json);
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
            data.user_name = userName;
            Save();
        }

        public void AddGold(int amount)
        {
            data.gold = Mathf.Max(0, data.gold + amount);
            Save();
        }

        public void SetGold(int gold)
        {
            data.gold = Mathf.Max(0, gold);
            Save();
        }

        public void SetMainStats(int maxHp, int physicalResist, int magicalResist)
        {
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
            data.sub_stats.speed = speed;
            data.sub_stats.poison_resist = poisonResist;
            data.sub_stats.electric_resist = electricResist;
            data.sub_stats.amber_resist = amberResist;
            data.sub_stats.freeze_resist = freezeResist;
            Save();
        }

        public string GetSavePath()
        {
            return PersistentPath;
        }
    }
}