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
        if (string.IsNullOrEmpty(coord) || go == null) return;
        map[coord] = go;
        Debug.Log($"[Registry] Register {coord} -> {go.name}");
    }

    public bool TryGet(string coord, out GameObject go) => map.TryGetValue(coord, out go);
}
}