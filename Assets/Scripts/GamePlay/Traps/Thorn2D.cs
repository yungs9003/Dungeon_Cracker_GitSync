using System.Collections.Generic;
using UnityEngine;
using Dungeon.Player;
using Dungeon.Core;

namespace Dungeon.Traps
{
    [RequireComponent(typeof(Collider2D))]
    public class Thorn2D : MonoBehaviour, ITrapToggle
    {
        [Header("Type")]
        [SerializeField] private Dungeon.Core.DamageType damageType = Dungeon.Core.DamageType.Physical;

        [Header("Damage")]
        [SerializeField] private int damage = 2;
        [SerializeField] private int bleedingDamage = 5;
        [SerializeField] private float hitCooldown = 0.5f;

        [Header("Knockback")]
        [SerializeField] private float knockbackDistance = 0.6f;
        [SerializeField] private float knockbackDuration = 0.08f;

        [Header("Target Filter (optional)")]
        [SerializeField] private LayerMask targetMask;

        [Header("Trap Toggle")]
        [SerializeField] private bool trapEnabled = true;

        private readonly Dictionary<Health, float> nextDamageTime = new();

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        public void SetTrapEnabled(bool enabled)
        {
            trapEnabled = enabled;
            if (!trapEnabled) nextDamageTime.Clear();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryDamage(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryDamage(other);
        }

        private void TryDamage(Collider2D other)
        {
            if (!trapEnabled) return;

            if (targetMask.value != 0 &&
                (targetMask.value & (1 << other.gameObject.layer)) == 0)
                return;

            Health health = other.GetComponentInParent<Health>();
            if (health == null) return;

            float now = Time.time;

            if (nextDamageTime.TryGetValue(health, out float allowedTime) && now < allowedTime)
                return;

            // 즉시 데미지 적용
            health.TakeDamage(damage, damageType);
            // 출혈 상태 유발: tick당 bleedingDamage 사용
            health.TakeBleedingDamage(bleedingDamage, 7f, 1f);
            nextDamageTime[health] = now + hitCooldown;

            var kb = other.GetComponentInParent<KnockbackReceiver2D>();
            if (kb != null)
            {
                Vector2 dir = (other.transform.position - transform.position);
                if (dir.sqrMagnitude < 1e-6f)
                    dir = Vector2.up;

                kb.AddKnockback(dir, knockbackDistance, knockbackDuration);
            }
        }
    }
}