using System.Collections.Generic;
using Dungeon.Core;
using UnityEngine;

namespace Dungeon.Traps
{
    public sealed class IceFloorTrap2D : MonoBehaviour, ITrapToggle, IPlayerDestructionGate
    {
        private static readonly HashSet<IceFloorTrap2D> surfaces = new HashSet<IceFloorTrap2D>();
        [SerializeField] private Vector2 surfaceSize = Vector2.one;
        [SerializeField, Range(1f, 1.5f)] private float maxSpeedMultiplier = 1.5f;
        [SerializeField, Min(.01f)] private float acceleration = 1f;
        [SerializeField, Min(.01f)] private float releaseDeceleration = .4f;
        [SerializeField, Min(.01f)] private float reverseBraking = 2.5f;
        private bool trapEnabled = true;
        public float MaxSpeedMultiplier => Mathf.Clamp(maxSpeedMultiplier, 1f, 1.5f);
        public float Acceleration => acceleration;
        public float ReleaseDeceleration => releaseDeceleration;
        public float ReverseBraking => reverseBraking;
        public bool CanBeDestroyedByPlayer() => false;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSurfaces() => surfaces.Clear();
        private void OnEnable() => surfaces.Add(this);
        private void OnDisable() => surfaces.Remove(this);
        public void SetTrapEnabled(bool value) { trapEnabled = value; }
        public bool Contains(Vector2 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            return Mathf.Abs(local.x) <= surfaceSize.x * .5f + .0001f && Mathf.Abs(local.y) <= surfaceSize.y * .5f + .0001f;
        }
        public static bool TryGetSurface(Vector2 position, out IceFloorTrap2D surface)
        {
            surface = null;
            foreach (var candidate in surfaces)
            {
                if (candidate == null || !candidate.isActiveAndEnabled || !candidate.trapEnabled || !candidate.Contains(position)) continue;
                if (surface == null || candidate.GetInstanceID() < surface.GetInstanceID()) surface = candidate;
            }
            return surface != null;
        }
    }
}
