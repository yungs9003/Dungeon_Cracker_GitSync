using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.Core;

public class DungeonDatabase : MonoBehaviour
{
    public static DungeonDatabase Instance { get; private set; }

    [Header("Resources JSON Path (no extension)")]
    [SerializeField] private string catalogResourcePath = "Data/dungeons";

    private Dictionary<string, DungeonInfo> map = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadCatalog();
    }

    private void LoadCatalog()
    {
        var ta = Resources.Load<TextAsset>(catalogResourcePath);
        if (ta == null)
        {
            Debug.LogError($"[DungeonDatabase] Catalog not found in Resources: '{catalogResourcePath}.json'");
            return;
        }

        var catalog = JsonUtility.FromJson<DungeonCatalog>(ta.text);
        map.Clear();

        if (catalog?.dungeons == null) return;

        foreach (var d in catalog.dungeons)
        {
            if (string.IsNullOrEmpty(d.id)) continue;
            map[d.id] = d;
        }
    }

    public bool TryGetDungeon(string dungeonId, out DungeonInfo info)
        => map.TryGetValue(dungeonId, out info);
}
