using UnityEngine;
using Dungeon.Core;

namespace Dungeon.Traps
{
    public class Bow2D : MonoBehaviour, ITrapToggle
    {
        [Header("Projectile Prefab")]
        [SerializeField] private Arrow2D projectilePrefab;

        [Header("Muzzle")]
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private Vector2 muzzleLocalOffset = new Vector2(0.6f, 0f);

        [Header("Stats")]
        [SerializeField] private float speed = 4f;
        [SerializeField] private int damage = 5;
        [SerializeField] private float fireCooldown = 3f;
        [SerializeField] private bool autoFire = true;

        [Header("Spawn")]
        [SerializeField] private float forwardPadding = 0.15f;
        [SerializeField] private Collider2D ownerCollider;

        private float lastFireTime;

        public void SetTrapEnabled(bool enabled) => autoFire = enabled;

        private void Awake()
        {
            if (ownerCollider == null)
                ownerCollider = GetComponent<Collider2D>();
        }

        private void Update()
        {
            if (!autoFire) return;

            if (Time.time - lastFireTime >= fireCooldown)
            {
                lastFireTime = Time.time;
                Fire();
            }
        }

        public void Fire()
        {
            if (projectilePrefab == null) return;

            Vector2 dir = ((Vector2)transform.up).normalized;

            Vector2 spawnPos = muzzlePoint != null
                ? (Vector2)muzzlePoint.position
                : (Vector2)transform.TransformPoint(muzzleLocalOffset);

            spawnPos += dir * forwardPadding;

            Arrow2D arrow = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
            arrow.Launch(dir, speed, damage, ownerCollider);
        }
    }
}