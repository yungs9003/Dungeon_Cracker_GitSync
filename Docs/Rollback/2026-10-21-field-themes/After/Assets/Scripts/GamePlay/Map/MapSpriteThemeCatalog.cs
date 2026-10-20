using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.Map
{
    /// <summary>원본 Sprite별 테마 대응표. 빈 칸은 원본을 사용하며 나중에 Inspector에서 추가할 수 있습니다.</summary>
    [CreateAssetMenu(menuName = "Dungeon/Map Sprite Theme Catalog")]
    public sealed class MapSpriteThemeCatalog : ScriptableObject
    {
        [Serializable]
        private sealed class Entry
        {
            public Sprite original;
            public Sprite ice;
            public Sprite fire;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        private Dictionary<Sprite, Entry> lookup;
        private static MapSpriteThemeCatalog defaultCatalog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDefaultCatalog() => defaultCatalog = null;

        public static Sprite ResolveDefault(Sprite sprite, FieldEffectManager.FieldEffectType theme)
        {
            if (sprite == null) return null;
            if (defaultCatalog == null)
                defaultCatalog = Resources.Load<MapSpriteThemeCatalog>("Data/MapSpriteThemes");
            return defaultCatalog != null ? defaultCatalog.Resolve(sprite, theme) : sprite;
        }

        public Sprite Resolve(Sprite sprite, FieldEffectManager.FieldEffectType theme)
        {
            if (sprite == null) return null;
            if (lookup == null)
            {
                lookup = new Dictionary<Sprite, Entry>();
                foreach (var entry in entries)
                {
                    if (entry == null || entry.original == null) continue;
                    lookup[entry.original] = entry;
                    if (entry.ice != null) lookup[entry.ice] = entry;
                    if (entry.fire != null) lookup[entry.fire] = entry;
                }
            }
            if (!lookup.TryGetValue(sprite, out var match)) return sprite;
            Sprite themed = theme switch
            {
                FieldEffectManager.FieldEffectType.Ice => match.ice,
                FieldEffectManager.FieldEffectType.Burn => match.fire,
                _ => null
            };
            return themed != null ? themed : match.original;
        }

        private void OnEnable() => lookup = null;
        private void OnValidate() => lookup = null;
    }
}
