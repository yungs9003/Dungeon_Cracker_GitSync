using UnityEngine;
using Dungeon.Core;

namespace Dungeon.System
{
    // Component that handles damage-related gimmicks (bleeding, modifiers, etc.)
    public class DamageSystem : MonoBehaviour, IDamageSystem, IFieldEffectDamageReceiver
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
            ResolveHealth();

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

        public void StartBleeding(
            int amount,
            float duration = 7f,
            float tickInterval = 0.5f)
        {
            ResolveHealth();

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

        /// <summary>
        /// burn 필드의 1회 주기 피해를 적용합니다.
        /// 직접 피격이 아니므로 CountDirectHit()을 호출하지 않습니다.
        /// </summary>
        public void ApplyBurnFieldDamage(int amount)
        {
            ApplyFieldEffectDamage(amount);
        }

        /// <summary>
        /// ice 필드의 1회 주기 피해를 적용합니다.
        /// 직접 피격이 아니므로 CountDirectHit()을 호출하지 않습니다.
        /// </summary>
        public void ApplyIceFieldDamage(int amount)
        {
            ApplyFieldEffectDamage(amount);
        }

        /// <summary>
        /// 출혈/화상/냉기 같은 주기 피해의 공통 진입점입니다.
        /// 현재는 필드별 저항 계산 없이 그대로 피해를 적용합니다.
        /// </summary>
        private void ApplyFieldEffectDamage(int amount)
        {
            ResolveHealth();

            if (amount <= 0 || health == null || health.currentHP <= 0)
                return;

            // Bleeding과 같은 지속 피해이므로 직접 피격 횟수와 무적 프레임을 사용하지 않습니다.
            health.ApplyDamageDirect(amount);
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
                ApplyFieldEffectDamage(bleedingDamagePerTick);
                lastBleedingDamageTime = Time.time + bleedingTickInterval;
            }
        }

        public void ClearBleeding()
        {
            isBleedingActive = false;
            bleedingEndTime = 0f;
            lastBleedingDamageTime = 0f;
        }

        private void ResolveHealth()
        {
            if (health == null)
                health = GetComponent(typeof(IDamageReceiver)) as IDamageReceiver;
        }
    }
}
