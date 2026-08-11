using UnityEngine;
using UnityEngine.Events;
using Dungeon.Core;

namespace Dungeon.Player
{
    public class Health : MonoBehaviour, IDamageReceiver
    {
        [Header("Stats")]
        public int maxHP = 3;
        public int currentHP { get; private set; }

        [Header("Save File")]
        [SerializeField] private bool loadMaxHpFromSaveFile = true;
        [SerializeField] private StatusSaveFile saveFile;

        [Header("Events")]
        public UnityEvent<int, int> onHealthChanged;
        public UnityEvent onDied;

        [Header("Invincibility (i-frames)")]
        [SerializeField] private bool useInvincibility = true;
        [SerializeField] private float invincibilityAfterHit = 0.35f;

        private float invincibleUntil = 0f;
        public bool IsInvincible => useInvincibility && Time.time < invincibleUntil;

        private IDamageSystem damageSystem;

        // Local bleeding fallback (used when no IDamageSystem is attached)
        private bool isBleedingActive = false;
        private float bleedingEndTime = 0f;
        private int bleedingDamagePerTick = 1;
        private float bleedingTickInterval = 0.5f;
        private float lastBleedingDamageTime = 0f;

        private bool initializedFromSaveFile = false;

        private void Awake()
        {
            ResolveSaveFileReference();

            if (saveFile != null && saveFile.Data != null)
            {
                ApplySaveData(saveFile.Data, fillCurrentHp: true);
            }
            else
            {
                currentHP = Mathf.Max(1, maxHP);
                onHealthChanged?.Invoke(currentHP, maxHP);
            }
        }

        public void GrantInvincibility(float duration)
        {
            if (!useInvincibility) return;
            if (duration <= 0f) return;

            invincibleUntil = Mathf.Max(invincibleUntil, Time.time + duration);
        }

        private void OnEnable()
        {
            ResolveSaveFileReference();

            if (saveFile != null)
            {
                saveFile.OnSaveDataChanged -= OnSaveDataChanged;
                saveFile.OnSaveDataChanged += OnSaveDataChanged;

                if (saveFile.Data != null)
                    ApplySaveData(saveFile.Data, fillCurrentHp: !initializedFromSaveFile);
            }
        }

        private void OnDisable()
        {
            if (saveFile != null)
                saveFile.OnSaveDataChanged -= OnSaveDataChanged;
        }

        private void ResolveSaveFileReference()
        {
            if (StatusSaveFile.Instance != null)
            {
                saveFile = StatusSaveFile.Instance;
                return;
            }

            saveFile = FindFirstObjectByType<StatusSaveFile>();
        }

        private void OnSaveDataChanged(StatusSaveData data)
        {
            ApplySaveData(data, fillCurrentHp: !initializedFromSaveFile);
        }

        private void ApplySaveData(StatusSaveData data, bool fillCurrentHp)
        {
            if (data == null)
                return;

            if (!loadMaxHpFromSaveFile)
            {
                if (!initializedFromSaveFile)
                {
                    currentHP = Mathf.Max(1, maxHP);
                    initializedFromSaveFile = true;
                    onHealthChanged?.Invoke(currentHP, maxHP);
                }

                return;
            }

            if (data.main_stats == null)
                return;

            int previousMaxHp = maxHP;
            int savedMaxHp = Mathf.Max(1, data.main_stats.max_HP);

            maxHP = savedMaxHp;

            if (fillCurrentHp)
            {
                currentHP = maxHP;
            }
            else
            {
                int currentBeforeAdjustment = currentHP;

                if (maxHP > previousMaxHp)
                {
                    int hpIncrease = maxHP - previousMaxHp;
                    currentHP = Mathf.Min(maxHP, currentBeforeAdjustment + hpIncrease);
                }
                else
                {
                    currentHP = Mathf.Clamp(currentBeforeAdjustment, 0, maxHP);
                }
            }

            initializedFromSaveFile = true;
            onHealthChanged?.Invoke(currentHP, maxHP);
        }

        // 기존 호출 호환용: 타입을 지정하지 않으면 Physical로 처리
        public void TakeDamage(int amount)
        {
            TakeDamage(amount, DamageType.Physical);
        }

        // 타입 기반 분기 지원 (물리/마법/True)
        public void TakeDamage(int amount, DamageType type)
        {
            EnsureDamageSystem();

            if (damageSystem != null)
            {
                damageSystem.TakeDamage(amount, type);
                return;
            }

            // Fallback: apply directly if DamageSystem unavailable
            if (amount <= 0 || currentHP <= 0) return;
            if (IsInvincible) return;
            CountDirectHit();
            int finalDamage = amount;
            currentHP = Mathf.Max(0, currentHP - finalDamage);
            onHealthChanged?.Invoke(currentHP, maxHP);
            if (useInvincibility && invincibilityAfterHit > 0f && currentHP > 0)
                GrantInvincibility(invincibilityAfterHit);
            if (currentHP == 0)
                Die();
        }

        // Apply damage already computed (used by DamageSystem)
        public void ApplyDamageDirect(int finalDamage)
        {
            if (finalDamage <= 0 || currentHP <= 0) return;

            currentHP = Mathf.Max(0, currentHP - finalDamage);
            onHealthChanged?.Invoke(currentHP, maxHP);

            if (useInvincibility && invincibilityAfterHit > 0f && currentHP > 0)
                GrantInvincibility(invincibilityAfterHit);

            if (currentHP == 0)
                Die();
        }

        public void TakeBleedingDamage(int amount, float duration = 7f, float tickInterval = 0.5f)
        {
            EnsureDamageSystem();
            if (damageSystem != null)
            {
                damageSystem.StartBleeding(amount, duration, tickInterval);
                return;
            }

            // Fallback: local bleeding if no external damage system is present
            if (amount <= 0 || currentHP <= 0) return;

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

        public void Heal(int amount)
        {
            if (amount <= 0 || currentHP <= 0) return;

            currentHP = Mathf.Min(maxHP, currentHP + amount);
            onHealthChanged?.Invoke(currentHP, maxHP);
        }

        private void Update()
        {
            // If an external damage system handles bleeding, prefer it. Otherwise handle local bleeding.
            if (damageSystem == null)
            {
                if (isBleedingActive && Time.time >= bleedingEndTime)
                    isBleedingActive = false;

                if (isBleedingActive && Time.time >= lastBleedingDamageTime)
                {
                    currentHP = Mathf.Max(0, currentHP - bleedingDamagePerTick);
                    onHealthChanged?.Invoke(currentHP, maxHP);

                    lastBleedingDamageTime = Time.time + bleedingTickInterval;

                    if (currentHP == 0)
                        Die();
                }
            }
        }

        public void CountDirectHit()
        {
            StatusSaveFile file = StatusSaveFile.Instance;

            if (file == null)
            {
                Debug.LogWarning("[Health] Cannot count hit. StatusSaveFile.Instance is missing.");
                return;
            }

            file.AddHitCount(1);
        }

        private void Die()
        {
            onDied?.Invoke();
        }

        public void ReviveFull()
        {
            ResolveSaveFileReference();

            if (saveFile != null && saveFile.Data != null)
                ApplySaveData(saveFile.Data, fillCurrentHp: true);
            else
                currentHP = maxHP;

            onHealthChanged?.Invoke(currentHP, maxHP);
        }

        public void ClearInvincibility()
        {
            invincibleUntil = 0f;
        }

        private void EnsureDamageSystem()
        {
            if (damageSystem != null) return;

            damageSystem = GetComponent(typeof(IDamageSystem)) as IDamageSystem;
        }
    }
}