using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dungeon.Core;
using Dungeon.Traps;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>현재 맵 목록/grid/3F의 실제 파괴 가능 프리팹을 집계해 던전 DB에 기록합니다.</summary>
public static class DungeonTrapCountBuilder
{
    public const string DatabasePath = "Assets/Resources/Data/db.json";

    public static DungeonMapTrapCount[] Count(DungeonDefinition dungeon, IEnumerable<TrapDefinition> traps)
    {
        var allowed = new HashSet<string>(traps.Where(t => t != null && t.destructible == true).Select(t => t.code));
        var result = new List<DungeonMapTrapCount>();
        string path = !string.IsNullOrWhiteSpace(dungeon.mapResourcePath) ? dungeon.mapResourcePath.Trim('/') :
            "Maps/" + DungeonProgressService.SceneKey(dungeon.sceneName);
        foreach (var map in DungeonMapCatalog.LoadPath(path))
        {
            var data = JObject.Parse(map.text);
            int count = 0;
            foreach (string layer in new[] { "grid", "3F", "floor3" })
            {
                var coords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var cell in data[layer] as JArray ?? new JArray())
                {
                    string code = ((string)cell["code"])?.Trim();
                    string coord = ((string)cell["coord"])?.Trim();
                    if (string.IsNullOrEmpty(coord) || !coords.Add(coord)) throw new InvalidDataException(map.name + ": missing/duplicate coord " + coord);
                    if (code == null || !allowed.Contains(code)) continue;
                    var prefab = Resources.Load<GameObject>("Prefabs/" + code);
                    if (prefab == null) throw new InvalidDataException(map.name + ": destructible prefab missing " + code);
                    if (!prefab.GetComponentsInChildren<Destructible>(true).Any(d => d.CountsAsBrokenTrap))
                        throw new InvalidDataException(map.name + ": DB/Destructible count setting mismatch " + code);
                    count++;
                }
            }
            result.Add(new DungeonMapTrapCount { map = map.name, destructible_trap_count = count });
        }
        if (result.Count == 0) throw new InvalidDataException(dungeon.sceneName + ": no map files");
        return result.ToArray();
    }

    [MenuItem("Tools/Dungeon/Update Destructible Trap Counts")]
    public static void UpdateDatabase()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play를 중지한 뒤 함정 수를 집계하세요.");
        var root = JObject.Parse(File.ReadAllText(DatabasePath));
        var definitions = root.ToObject<DbRoot>();
        // 모든 집계가 성공한 후에만 한 번 저장합니다.
        foreach (var dungeon in definitions.dungeons ?? Array.Empty<DungeonDefinition>())
        {
            var counts = Count(dungeon, definitions.traps ?? Array.Empty<TrapDefinition>());
            var row = ((JArray)root["dungeons"]).OfType<JObject>().Single(d => (string)d["sceneName"] == dungeon.sceneName);
            row["map_trap_counts"] = JArray.FromObject(counts);
            row["destructible_trap_count"] = counts.Sum(c => c.destructible_trap_count);
        }
        File.WriteAllText(DatabasePath, root.ToString(Formatting.Indented) + "\n");
        AssetDatabase.ImportAsset(DatabasePath);
        if (GameDatabase.Instance != null) GameDatabase.Instance.LoadDatabase();
        Debug.Log("[DungeonTrapCount] 맵별/던전별 파괴 가능한 함정 수를 DB에 갱신했습니다.");
    }
}
