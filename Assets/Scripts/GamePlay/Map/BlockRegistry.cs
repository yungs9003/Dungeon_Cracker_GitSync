using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.Map
{
public class BlockRegistry : MonoBehaviour
{
    public static BlockRegistry Instance { get; private set; }

    private readonly Dictionary<string, GameObject> map = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Register(string coord, GameObject go)
    {
        if (string.IsNullOrWhiteSpace(coord) || go == null) return;
        map[coord.Trim().ToLowerInvariant()] = go;
        Debug.Log($"[Registry] Register {coord} -> {go.name}");
    }

    public bool TryGet(string coord, out GameObject go)
    {
        go = null;
        return !string.IsNullOrWhiteSpace(coord) && map.TryGetValue(coord.Trim().ToLowerInvariant(), out go) && go != null;
    }

    public void Unregister(string coord, GameObject expected)
    {
        if (string.IsNullOrWhiteSpace(coord)) return;
        coord = coord.Trim().ToLowerInvariant();
        if (map.TryGetValue(coord, out var current) && current == expected) map.Remove(coord);
    }
}
}
