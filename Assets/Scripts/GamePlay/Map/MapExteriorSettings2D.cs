using System;
using UnityEngine;

namespace Dungeon.Map
{
    [CreateAssetMenu(menuName = "Dungeon/Map Exterior Settings")]
    public sealed class MapExteriorSettings2D : ScriptableObject
    {
        [Serializable]
        public sealed class Background
        {
            public Color color = Color.black;
            public Sprite sprite;
            public bool tiled = true;
            [Min(0.01f)] public float tileWorldSize = 1f;
        }
        public Background normal = new() { color = new Color32(97, 86, 80, 255) };
        public Background ice = new() { color = new Color32(43, 131, 163, 255) };
        public Background fire = new() { color = new Color32(61, 61, 61, 255) };
        public Background Get(FieldEffectManager.FieldEffectType theme) => theme switch
        {
            FieldEffectManager.FieldEffectType.Ice => ice,
            FieldEffectManager.FieldEffectType.Burn => fire,
            _ => normal
        };
    }
}
