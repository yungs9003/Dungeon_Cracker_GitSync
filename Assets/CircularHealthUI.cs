using UnityEngine;
using UnityEngine.UI;

public class CircularHealthUI : MonoBehaviour
{
    [Header("참조")]
    public Health health;       // 플레이어의 Health 컴포넌트
    public Image ringImage;     // 원형 체력바 이미지

    [Header("색 설정")]
    public Color greenColor = Color.green;
    public Color yellowColor = Color.yellow;
    public Color redColor = Color.red;

    [Header("단계 설정")]
    [Tooltip("각 색상 단계당 칸 수 (예: 5칸)")]
    public int segmentsPerStage = 5;

    [Tooltip("색상 단계 수 (초록/노랑/빨강 이면 3)")]
    public int stageCount = 3;

    private void Awake()
    {
        // 참조 자동 연결 (인스펙터에서 안 넣었을 때 대비)
        if (ringImage == null)
            ringImage = GetComponent<Image>();

        if (health == null)
            health = FindObjectOfType<Health>();
    }

    private void OnEnable()
    {
        if (health != null)
        {
            // HP 변경 이벤트 구독
            health.onHealthChanged.AddListener(OnHealthChanged);
            // 초기 값으로 한번 갱신
            OnHealthChanged(health.currentHP, health.maxHP);
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.onHealthChanged.RemoveListener(OnHealthChanged);
        }
    }

    /// <summary>
    /// Health가 바뀔 때마다 호출됨
    /// </summary>
    private void OnHealthChanged(int current, int max)
    {
        if (max <= 0 || ringImage == null)
            return;

        // 1) 채워진 양 (0~1)
        float ratio = (float)current / max;
        ratio = Mathf.Clamp01(ratio);

        // --- 칸(세그먼트) 단위로 끊고 싶다면 ---
        int totalSegments = segmentsPerStage * stageCount;   // 예: 5 * 3 = 15칸
        int currentSegment = Mathf.RoundToInt(ratio * totalSegments);
        float step = 1f / totalSegments;
        ringImage.fillAmount = currentSegment * step;
        // ------------------------------------

        // 2) 색상 단계 결정 (초록/노랑/빨강)
        float stageRatio = (float)current / max;  // 0~1

        if (stageRatio > 2f / 3f)        // 상위 1/3 → 초록
        {
            ringImage.color = greenColor;
        }
        else if (stageRatio > 1f / 3f)   // 중간 1/3 → 노랑
        {
            ringImage.color = yellowColor;
        }
        else                             // 하위 1/3 → 빨강
        {
            ringImage.color = redColor;
        }

        // 체력이 0이면 아예 안 보이게 하고 싶으면:
        ringImage.enabled = (current > 0);
    }
}
