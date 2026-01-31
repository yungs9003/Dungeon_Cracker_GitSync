using System;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private PlayerSaveData data = new();

    private string SavePath => Path.Combine(Application.persistentDataPath, "player_save.json");

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(SavePath))
            {
                data = new PlayerSaveData();
                Save();
                return;
            }

            var json = File.ReadAllText(SavePath);
            data = JsonUtility.FromJson<PlayerSaveData>(json) ?? new PlayerSaveData();

            if (data.dungeons == null) data.dungeons = new();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Load failed: {e}");
            data = new PlayerSaveData();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Save failed: {e}");
        }
    }

    public DungeonProgress GetOrCreateDungeonProgress(string dungeonId)
    {
        var list = data.dungeons;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].dungeonId == dungeonId)
            {
                EnsureCriteriaLength(list[i]);
                return list[i];
            }
        }

        var created = new DungeonProgress
        {
            dungeonId = dungeonId,
            criteria = new bool[4],
            bestTimeMs = 0,
            clearCount = 0,
            lastPlayedUtc = ""
        };

        list.Add(created);
        return created;
    }

    private void EnsureCriteriaLength(DungeonProgress p)
    {
        if (p.criteria == null || p.criteria.Length != 4)
            p.criteria = new bool[4];
    }
}
