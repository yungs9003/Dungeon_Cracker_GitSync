using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Dungeon.Player;

namespace Dungeon.UI
{
public class BarHealthUI : MonoBehaviour
{
    [Header("Refs")]
    public Health health;
    public Image healthBar;     // 막대 이미지 (Filled 타입)
    public TMP_Text healthText; // 체력 텍스트 (현재/최대)

    [Header("Thresholds")]
    [Range(0f, 1f)] public float yellowThreshold = 0.5f; // 경고 시작 비율
    [Range(0f, 1f)] public float redThreshold = 0.15f;   // 위험 시작 비율

    private void Awake()
    {
        // Inspector에서 드래그해도 되고, 이름으로 자동 연결도 가능
        if (healthBar == null)
            healthBar = FindChildImageByName("HealthBar");

        if (health == null)
            health = FindAnyObjectByType<Health>();

        if (healthText == null)
        {
            var t = GetComponentInChildren<TMP_Text>(true);
            healthText = t;
        }
    }

    private void OnEnable()
    {
        if (health == null) return;
        health.onHealthChanged.AddListener(OnHealthChanged);
        OnHealthChanged(health.currentHP, health.maxHP);
    }

    private void OnDisable()
    {
        if (health == null) return;
        health.onHealthChanged.RemoveListener(OnHealthChanged);
    }

    private void OnHealthChanged(int current, int max)
    {
        if (max <= 0) return;
        if (healthBar == null) return;

        // 체력 비율 계산 (0.0 ~ 1.0)
        float healthRatio = (float)current / max;

        // 막대 길이 설정
        SetBar(healthBar, true, healthRatio);

        // 막대 색상 설정
        healthBar.color = GetBarColor(healthRatio);

        // 체력 텍스트 표시 (현재/최대)
        if (healthText != null)
            healthText.text = $"{current} / {max}";
    }

    /// <summary>
    /// 체력 비율에 따라 막대 색상을 결정합니다.
    /// - ratio > yellowThreshold: 초록색
    /// - ratio > redThreshold: 노란색
    /// - ratio ≤ redThreshold: 빨간색
    /// Inspector에서 yellowThreshold, redThreshold를 조정할 수 있습니다.
    /// </summary>
    private Color GetBarColor(float ratio)
    {
        float safeThreshold = Mathf.Clamp01(yellowThreshold);
        float dangerThreshold = Mathf.Clamp01(redThreshold);

        if (ratio > safeThreshold)
            return Color.green;
        else if (ratio > dangerThreshold)
            return Color.yellow;
        else
            return Color.red;
    }

    private void SetBar(Image img, bool enabled, float fill)
    {
        img.enabled = enabled;
        img.fillAmount = Mathf.Clamp01(fill);
    }

    private Image FindChildImageByName(string childName)
    {
        var images = GetComponentsInChildren<Image>(true);
        foreach (var img in images)
            if (img.name == childName) return img;
        return null;
    }
}
}
