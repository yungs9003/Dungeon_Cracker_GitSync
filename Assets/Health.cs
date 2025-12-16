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

    private void Awake()
    {
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        if (currentHP == 0) currentHP = maxHP; // 초기값을 max로
        onHealthChanged?.Invoke(currentHP, maxHP);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || currentHP <= 0) return;

        currentHP = Mathf.Max(0, currentHP - amount);
        onHealthChanged?.Invoke(currentHP, maxHP);

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
        // 여기서 Destroy(gameObject); 하거나, 애니메이션/드랍 처리 등
        // Destroy(gameObject);
    }
}
