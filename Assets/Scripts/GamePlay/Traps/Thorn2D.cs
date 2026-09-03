using System.Collections.Generic;
using UnityEngine;
using Dungeon.Player;
using Dungeon.Core;

namespace Dungeon.Traps
{
    [RequireComponent(typeof(Collider2D))]
    public class Thorn2D : MonoBehaviour, ITrapToggle
    {
        [Header("Hit")]
        [SerializeField] private float hitCooldown = 0.5f;

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

            TrapDamageEvent.Raise(gameObject, health);
            nextDamageTime[health] = now + hitCooldown;
        }
    }
}
