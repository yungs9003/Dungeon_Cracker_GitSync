using UnityEngine;
using UnityEngine.Events;
using Dungeon.Core;

namespace Dungeon.Player
{
public class Health : MonoBehaviour
{
    [Header("Stats")]
    public int maxHP = 3;
    public int currentHP { get; private set; }

    [Header("Save File")]
    [SerializeField] private bool loadMaxHpFromSaveFile = true;
    [SerializeField] private StatusSaveFile saveFile;

    [Header("Events")]
    public UnityEvent<int, int> onHealthChanged; // (current, max)
    public UnityEvent onDied;

    [Header("Invincibility (i-frames)")]
    [SerializeField] private bool useInvincibility = true;
    [SerializeField] private float invincibilityAfterHit = 0.35f;

    private float invincibleUntil = 0f;
    public bool IsInvincible => useInvincibility && Time.time < invincibleUntil;

    [Header("Bleeding")]
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
            // savefile을 아직 못 읽은 경우 임시로 Inspector 값을 사용
            currentHP = maxHP;
            onHealthChanged?.Invoke(currentHP, maxHP);
        }
    }

    private void OnEnable()
    {
        ResolveSaveFileReference();

        if (saveFile != null)
        {
            saveFile.OnSaveDataChanged += OnSaveDataChanged;

            // Awake 순서 문제로 나중에 연결된 경우 여기서 다시 한 번 적용
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
    // 이미 살아있는 진짜 싱글톤이 있으면 무조건 그걸 사용
        if (StatusSaveFile.Instance != null)
        {
            saveFile = StatusSaveFile.Instance;
            return;
        }
    // Instance가 아직 없을 때만 임시로 찾기
        saveFile = FindFirstObjectByType<StatusSaveFile>();
    }

    private void OnSaveDataChanged(StatusSaveData data)
    {
        // savefile이 나중에 로드되었을 때 처음 한 번은 currentHP도 maxHP로 채움
        ApplySaveData(data, fillCurrentHp: !initializedFromSaveFile);
    }

    private void ApplySaveData(StatusSaveData data, bool fillCurrentHp)
    {
        Debug.Log(
    $"[Health] ApplySaveData. " +
    $"Object={gameObject.name}, " +
    $"SaveFileID={(saveFile != null ? saveFile.GetInstanceID().ToString() : "null")}, " +
    $"maxHP from save={data.main_stats.max_HP}, " +
    $"currentHP before={currentHP}"
);
        if (!loadMaxHpFromSaveFile)
        {
            if (!initializedFromSaveFile)
            {
                currentHP = maxHP;
                initializedFromSaveFile = true;
                onHealthChanged?.Invoke(currentHP, maxHP);
            }

            return;
        }

        if (data == null || data.main_stats == null)
            return;

        int savedMaxHp = Mathf.Max(1, data.main_stats.max_HP);

        maxHP = savedMaxHp;

        if (fillCurrentHp)
        {
            currentHP = maxHP;
        }
        else
        {
            currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        }

        initializedFromSaveFile = true;
        onHealthChanged?.Invoke(currentHP, maxHP);
    }

    public void GrantInvincibility(float duration)
    {
        if (!useInvincibility) return;
        if (duration <= 0f) return;

        invincibleUntil = Mathf.Max(invincibleUntil, Time.time + duration);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || currentHP <= 0) return;
        if (IsInvincible) return;

        int finalDamage = amount;

        if (isBleedingActive)
            finalDamage = Mathf.RoundToInt(amount * 1.2f);

        currentHP = Mathf.Max(0, currentHP - finalDamage);
        onHealthChanged?.Invoke(currentHP, maxHP);

        if (useInvincibility && invincibilityAfterHit > 0f && currentHP > 0)
            GrantInvincibility(invincibilityAfterHit);

        if (currentHP == 0)
            Die();
    }

    public void TakeBleedingDamage(int amount, float duration = 7f, float tickInterval = 0.5f)
    {
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

    private void Die()
    {
        isBleedingActive = false;
        bleedingEndTime = 0f;
        lastBleedingDamageTime = 0f;

        onDied?.Invoke();
    }

    public void ReviveFull()
    {
        ResolveSaveFileReference();

        if (saveFile != null && saveFile.Data != null)
            ApplySaveData(saveFile.Data, fillCurrentHp: true);
        else
            currentHP = maxHP;

        isBleedingActive = false;
        bleedingEndTime = 0f;
        lastBleedingDamageTime = 0f;

        onHealthChanged?.Invoke(currentHP, maxHP);
    }

    public void ClearInvincibility()
    {
        invincibleUntil = 0f;
    }
}
}