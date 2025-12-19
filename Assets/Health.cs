using UnityEngine;
using UnityEngine.Events;

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

        currentHP = Mathf.Max(0, currentHP - amount);
        onHealthChanged?.Invoke(currentHP, maxHP);

        // ✅ 기본 피격 무적(원하면 Inspector에서 조절/0으로 비활성)
        if (useInvincibility && invincibilityAfterHit > 0f && currentHP > 0)
            GrantInvincibility(invincibilityAfterHit);

        if (currentHP == 0) Die();
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || currentHP <= 0) return;

        currentHP = Mathf.Min(maxHP, currentHP + amount);
        onHealthChanged?.Invoke(currentHP, maxHP);
    }

    private void Die()
    {
        onDied?.Invoke();
        // Destroy(gameObject);
    }

    public void ReviveFull()
    {
        currentHP = maxHP;
        onHealthChanged?.Invoke(currentHP, maxHP);
    }

    public void ClearInvincibility() // ✅ 추가 (원하면 사용)
    {
        invincibleUntil = 0f;
    }
}
