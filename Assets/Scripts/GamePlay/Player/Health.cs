using UnityEngine;
using UnityEngine.Events;

namespace Dungeon.Player
{
public class Health : MonoBehaviour
{
    [Header("Stats")]
    public int maxHP = 3;
    public int currentHP { get; private set; }

    [Header("Events")]
    public UnityEvent<int, int> onHealthChanged; // (current, max)
    public UnityEvent onDied;

    [Header("Invincibility (i-frames)")] // ✅ 추가
    [SerializeField] private bool useInvincibility = true;
    [SerializeField] private float invincibilityAfterHit = 0.35f; // 기본 피격 무적시간(원하면 0으로)

    private float invincibleUntil = 0f; // ✅ 추가

    public bool IsInvincible => useInvincibility && Time.time < invincibleUntil; // ✅ 추가

    [Header("Bleeding")]
    private bool isBleedingActive = false;
    private float bleedingEndTime = 0f;
    private int bleedingDamagePerTick = 1;
    private float bleedingTickInterval = 0.5f;
    private float lastBleedingDamageTime = 0f;

    private void Awake()
    {
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        if (currentHP == 0) currentHP = maxHP; // 초기값을 max로
        onHealthChanged?.Invoke(currentHP, maxHP);
    }

    /// <summary>
    /// 외부에서 강제로 무적 부여하고 싶을 때 호출
    /// </summary>
    public void GrantInvincibility(float duration) // ✅ 추가
    {
        if (!useInvincibility) return;
        if (duration <= 0f) return;

        invincibleUntil = Mathf.Max(invincibleUntil, Time.time + duration);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || currentHP <= 0) return;

        // ✅ 무적이면 데미지 무시
        if (IsInvincible) return;

        // 출혈 상태면 데미지 1.2배 적용
        int finalDamage = amount;
        if (isBleedingActive)
        {
            finalDamage = Mathf.RoundToInt(amount * 1.2f);
        }

        currentHP = Mathf.Max(0, currentHP - finalDamage);
        onHealthChanged?.Invoke(currentHP, maxHP);

        // ✅ 기본 피격 무적(원하면 Inspector에서 조절/0으로 비활성)
        if (useInvincibility && invincibilityAfterHit > 0f && currentHP > 0)
            GrantInvincibility(invincibilityAfterHit);

        if (currentHP == 0) Die();
    }

    public void TakeBleedingDamage(int amount, float duration = 7f, float tickInterval = 0.5f)
    {
        if (amount <= 0 || currentHP <= 0) return;
        // 이미 출혈 중이면 지속시간을 리셋하고 데미지/틱 설정을 갱신
        if (isBleedingActive)
        {
            bleedingDamagePerTick = amount;
            bleedingTickInterval = tickInterval;
            // 출혈 유지 시간을 현재 시점부터 새로 설정
            bleedingEndTime = Time.time + duration;
            return;
        }

        // 출혈 상태 시작 (무적 여부와 관계없이 시작)
        isBleedingActive = true;
        bleedingDamagePerTick = amount;
        bleedingTickInterval = tickInterval;
        bleedingEndTime = Time.time + duration;
        // 첫 출혈 틱은 tickInterval 뒤에 발생하도록 지연 (초기 즉시 데미지는 없음)
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
        // 출혈이 끝났는지 확인
        if (isBleedingActive && Time.time >= bleedingEndTime)
        {
            isBleedingActive = false;
        }

        // 출혈 상태일 때 주기적으로 데미지 적용
        if (isBleedingActive && Time.time >= lastBleedingDamageTime)
        {
            currentHP = Mathf.Max(0, currentHP - bleedingDamagePerTick);
            onHealthChanged?.Invoke(currentHP, maxHP);
            lastBleedingDamageTime = Time.time + bleedingTickInterval;

            if (currentHP == 0) Die();
        }
    }

    private void Die()
    {
        // 출혈 상태 초기화: 죽은 뒤 출혈이 남아있지 않도록 함
        isBleedingActive = false;
        bleedingEndTime = 0f;
        lastBleedingDamageTime = 0f;

        onDied?.Invoke();
        // Destroy(gameObject);
    }

    public void ReviveFull()
    {
        currentHP = maxHP;
        // 출혈 초기화: 부활 시 출혈 효과 제거
        isBleedingActive = false;
        bleedingEndTime = 0f;
        lastBleedingDamageTime = 0f;

        onHealthChanged?.Invoke(currentHP, maxHP);
    }

    public void ClearInvincibility() // ✅ 추가 (원하면 사용)
    {
        invincibleUntil = 0f;
    }
}
}