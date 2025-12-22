using UnityEngine;
using UnityEngine.UI;

public class CircularHealthUI : MonoBehaviour
{
    [Header("Refs")]
    public Health health;

    // 위->아래 그려짐: Green(최상단), Yellow, Red(최하단)
    public Image ringGreen;
    public Image ringYellow;
    public Image ringRed;
    public Image dividerMask; // DividerMask의 Image (Filled + Mask)

    [Header("Stage")]
    [Min(1)] public int segmentsPerStage = 5; // 5칸
    [Min(1)] public int stageCount = 3;       // 3단계(초/노/빨)

    private void Awake()
    {
        // Inspector에서 드래그해도 되고, 이름으로 자동 연결도 가능
        if (ringGreen == null)  ringGreen  = FindChildImageByName("HPbar Green");
        if (ringYellow == null) ringYellow = FindChildImageByName("HPbar Yellow");
        if (ringRed == null)    ringRed    = FindChildImageByName("HPbar Red");
        if (dividerMask == null) dividerMask = FindChildImageByName("DividerMask");


        if (health == null)
            health = FindObjectOfType<Health>();
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
        if (ringGreen == null || ringYellow == null || ringRed == null) return;

        int seg = Mathf.Max(1, segmentsPerStage);

        // HP 0이면 전부 숨김(원하면 red만 0으로 두고 켜둬도 됨)
        if (current <= 0)
        {
            SetRing(ringGreen, false, 0f);
            SetRing(ringYellow, false, 0f);
            SetRing(ringRed, false, 0f);
            SetRing(dividerMask, false, 0f);
            return;
        }

        // stage: 0=빨강(1~5), 1=노랑(6~10), 2=초록(11~15)  (seg=5 기준)
        int stage = Mathf.Clamp((current - 1) / seg, 0, stageCount - 1);

        // 해당 stage에서 남은 칸 수: 1..seg
        int stageHP = current - stage * seg;       // 1..seg
        stageHP = Mathf.Clamp(stageHP, 0, seg);

        // 딱 1/5 단위로 줄어드는 fill (1.0, 0.8, 0.6, 0.4, 0.2)
        float fill = stageHP / (float)seg;

        // 아래 레이어는 “다음 색이 보이도록” 항상 꽉 채워두고,
        // 현재 단계 링만 fill을 줄인다.
        if (stage >= 2) // 초록 단계
        {
            SetRing(dividerMask, true, 1f);
            SetRing(ringRed, true, 1f);
            SetRing(ringYellow, true, 1f);
            SetRing(ringGreen, true, fill);
        }
        else if (stage == 1) // 노랑 단계
        {
            SetRing(dividerMask, true, 1f);
            SetRing(ringRed, true, 1f);
            SetRing(ringYellow, true, fill);
            SetRing(ringGreen, false, 0f);
        }
        else // 빨강 단계
        {
            SetRing(dividerMask, true, fill);
            SetRing(ringRed, true, fill);
            SetRing(ringYellow, false, 0f);
            SetRing(ringGreen, false, 0f);
        }
    }

    private void SetRing(Image img, bool enabled, float fill)
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
