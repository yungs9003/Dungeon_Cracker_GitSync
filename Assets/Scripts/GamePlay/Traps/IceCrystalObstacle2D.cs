using System.Collections.Generic;
using Dungeon.Core;
using Dungeon.Player;
using UnityEngine;

namespace Dungeon.Traps
{
    [RequireComponent(typeof(Collider2D), typeof(Destructible))]
    public sealed class IceCrystalObstacle2D : MonoBehaviour, ITrapToggle, IPlayerDestructionGate
    {
        [SerializeField, Min(.1f)] private float contactDamageInterval = .5f;
        private ThemeTrapDamage2D damage;
        private ThemeTrapDamage2D ownDamage;
        private bool trapEnabled = true;
        private readonly Dictionary<Health, float> nextHit = new Dictionary<Health, float>();
        public bool CanBeDestroyedByPlayer() => true;
        public void SetDamageSource(ThemeTrapDamage2D source) { damage = source; }
        private void OnCollisionEnter2D(Collision2D collision) => Contact(collision.collider);
        private void OnCollisionStay2D(Collision2D collision) => Contact(collision.collider);
        private void Contact(Collider2D other)
        {
            if (!trapEnabled) return;
            Health health = other.GetComponentInParent<Health>();
            if (health == null || PlayerEffectLock2D.IsProtectedTarget(health)) return;
            if (nextHit.TryGetValue(health, out float time) && Time.time < time) return;
            if (ownDamage == null) ownDamage = GetComponent<ThemeTrapDamage2D>();
            if (damage == null) damage = GetComponentInParent<ThemeTrapDamage2D>();
            ThemeTrapDamage2D source = ownDamage != null && ownDamage.TryGetDefinition(out _) ? ownDamage : damage;
            if (source != null && source.Hit(health)) nextHit[health] = Time.time + contactDamageInterval;
        }
        public void SetTrapEnabled(bool value) { trapEnabled = value; GetComponent<Collider2D>().enabled = value; }
    }
}
