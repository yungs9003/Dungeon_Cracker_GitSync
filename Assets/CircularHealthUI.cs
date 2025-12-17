using UnityEngine;
using UnityEngine.UI;

public class CircularHealthUI : MonoBehaviour
{
    [Header("참조")]
    public Health health;       // 플레이어의 Health 컴포넌트
    public Image ringGreen;     // 원형 체력바 이미지 (제일 위)
    public Image ringYellow;
    public Image ringRed;

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
        // 이름으로 찾기(자식 전체에서 찾음)
        if (ringGreen == null)
            ringGreen = FindChildImageByName("HPbar Green");

        if (ringYellow == null)
            ringYellow = FindChildImageByName("HPbar Yellow");

        if (ringRed == null)
            ringRed = FindChildImageByName("HPbar Red");

        // 색상 기본값 적용(원하면 Inspector에서 빼도 됨)
        if (ringGreen != null) ringGreen.color = SetColorKeepAlpha(ringGreen.color, greenColor);
        if (ringYellow != null) ringYellow.color = SetColorKeepAlpha(ringYellow.color, yellowColor);
        if (ringRed != null) ringRed.color = SetColorKeepAlpha(ringRed.color, redColor);

        if (health == null)
            health = FindObjectOfType<Health>();

        if (ringGreen == null || ringYellow == null || ringRed == null)
            Debug.LogWarning("[CircularHealthUI] 링 Image 참조가 일부 비어 있습니다. (HPbar Green/Yellow/Red 이름 또는 계층 구조 확인)");
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.onHealthChanged.AddListener(OnHealthChanged);
            OnHealthChanged(health.currentHP, health.maxHP);
        }
    }

    private void OnDisable()
    {
        if (health != null)
            health.onHealthChanged.RemoveListener(OnHealthChanged);
    }

    private void OnHealthChanged(int current, int max)
    {
        if (max <= 0) return;

        // 링이 하나라도 없으면 크래시 방지
        if (ringGreen == null || ringYellow == null || ringRed == null) return;

        int seg = Mathf.Max(1, segmentsPerStage);

        // 0 이하면 전부 숨김(취향)
        if (current <= 0)
        {
            SetAlpha(ringGreen, 0f);
            SetAlpha(ringYellow, 0f);
            SetAlpha(ringRed, 0f);
            return;
        }

        // 현재가 몇 번째 단계인지 (0=빨강, 1=노랑, 2=초록) - maxHP=15, seg=5 기준
        int stage = Mathf.Clamp((current - 1) / seg, 0, stageCount - 1);

        // 현재 단계 안에서의 비율 (1 -> 0.8 -> 0.6 ... 처럼 감소)
        float t = (current - stage * seg) / (float)seg; // 0~1
        t = Mathf.Clamp01(t);

        // 위에 있는 레이어부터 투명해지게 (초록 -> 노랑 -> 빨강)
        // stage=2(초록 구간): 초록만 t로 감소, 노랑/빨강은 1
        // stage=1(노랑 구간): 노랑만 t로 감소, 초록은 0, 빨강은 1
        // stage=0(빨강 구간): 빨강만 t로 감소, 초록/노랑은 0
        float greenA  = (stage >= 2) ? t : 0f;
        float yellowA = (stage == 1) ? t : (stage > 1 ? 1f : 0f);
        float redA    = (stage == 0) ? t : 1f;

        SetAlpha(ringGreen, greenA);
        SetAlpha(ringYellow, yellowA);
        SetAlpha(ringRed, redA);
    }

    private void SetAlpha(Image img, float a)
    {
        if (img == null) return;
        var c = img.color;
        c.a = Mathf.Clamp01(a);
        img.color = c;
    }

    // (옵션) 링의 RGB는 설정값으로, 알파는 유지
    private Color SetColorKeepAlpha(Color original, Color targetRGB)
    {
        targetRGB.a = original.a;
        return targetRGB;
    }

    // 자식 전체에서 이름으로 Image 찾기
    private Image FindChildImageByName(string childName)
    {
        var images = GetComponentsInChildren<Image>(true);
        foreach (var img in images)
        {
            if (img.name == childName)
                return img;
        }
        return null;
    }
}
