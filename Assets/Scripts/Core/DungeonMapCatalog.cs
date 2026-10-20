using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Dungeon.Core
{
    /// <summary>팝업/입장/실제 맵 실행기가 공유하는 맵 목록입니다.</summary>
    public static class DungeonMapCatalog
    {
        public static string ResourcePath(string sceneName, string root = "Maps")
        {
            if (GameDatabase.TryFindDungeonBySceneName(sceneName, out var definition) &&
                !string.IsNullOrWhiteSpace(definition.mapResourcePath)) return definition.mapResourcePath.Trim('/');
            return root.TrimEnd('/') + "/" + DungeonProgressService.SceneKey(sceneName);
        }

        public static TextAsset[] Load(string sceneName, string root = "Maps") =>
            Resources.LoadAll<TextAsset>(ResourcePath(sceneName, root))
                .Where(asset => Regex.IsMatch(asset.name, @"^map_\d+$", RegexOptions.IgnoreCase))
                .OrderBy(asset => long.TryParse(asset.name.Substring(4), out long index) ? index : long.MaxValue)
                .ThenBy(asset => asset.name, StringComparer.Ordinal).ToArray();
    }
}
