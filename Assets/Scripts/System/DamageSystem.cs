using UnityEngine;
using Dungeon.Core;

namespace Dungeon.System
{
    // Component that handles damage-related gimmicks (bleeding, modifiers, etc.)
    public class DamageSystem : MonoBehaviour, IDamageSystem
    {
        private IDamageReceiver health;

        // Bleeding state (per-target)
        private bool isBleedingActive = false;
        private float bleedingEndTime = 0f;
        private int bleedingDamagePerTick = 1;
        private float bleedingTickInterval = 0.5f;
        private float lastBleedingDamageTime = 0f;

        public void Initialize(IDamageReceiver damageReceiver)
        {
            health = damageReceiver;
        }

        public void TakeDamage(int amount, DamageType type)
        {
            if (health == null)
                health = GetComponent(typeof(IDamageReceiver)) as IDamageReceiver;

            if (amount <= 0 || health == null || health.currentHP <= 0)
                return;

            if (health.IsInvincible)
                return;

            // Count direct hit (delegated to Health for save tracking)
            health.CountDirectHit();

            int finalDamage = amount;

            // Example gimmick: bleeding increases incoming damage
            if (isBleedingActive)
                finalDamage = Mathf.RoundToInt(amount * 1.2f);

            // Type-based hooks for future extensions
            switch (type)
            {
                case DamageType.Physical:
                    break;
                case DamageType.Magical:
                    break;
                case DamageType.True:
                    break;
            }

            health.ApplyDamageDirect(finalDamage);
        }

        public void StartBleeding(int amount, float duration = 7f, float tickInterval = 0.5f)
        {
            if (health == null)
                health = GetComponent(typeof(IDamageReceiver)) as IDamageReceiver;

            if (amount <= 0 || health == null || health.currentHP <= 0)
                return;

            if (isBleedingActive)
            {
                bleedingDamagePerTick = amount;
                bleedingTickInterval = tickInterval;
                bleedingEndTime = Time.time + duration;
                return;
            }

            isBleedingActive = true;
            bleedingDamagePerTick = amount;
            bleedingTickInterval = tickInterval;
            bleedingEndTime = Time.time + duration;
            lastBleedingDamageTime = Time.time + bleedingTickInterval;
        }

        private void Update()
        {
            if (!isBleedingActive)
                return;

            if (Time.time >= bleedingEndTime)
            {
                isBleedingActive = false;
                return;
            }

            if (Time.time >= lastBleedingDamageTime)
            {
                if (health != null)
                {
                    health.ApplyDamageDirect(bleedingDamagePerTick);
                }

                lastBleedingDamageTime = Time.time + bleedingTickInterval;
            }
        }

        public void ClearBleeding()
        {
            isBleedingActive = false;
            bleedingEndTime = 0f;
            lastBleedingDamageTime = 0f;
        }
    }
}
