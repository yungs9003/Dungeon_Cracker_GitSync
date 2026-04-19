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
        public Image healthCircle;      // 원형 체력 게이지 이미지 (Filled 타입)
        public TMP_Text healthText;     // 체력 텍스트 (현재/최대)
        public CircularDividersUI dividersUI; // 원형 Divider 제어 UI

        [Header("Thresholds")]
        [Range(0f, 1f)] public float yellowThreshold = 0.5f; // 경고 시작 비율
        [Range(0f, 1f)] public float redThreshold = 0.15f;   // 위험 시작 비율

        [Header("Circle")]
        [Range(0, 3)] public int fillOrigin = 2; // 0=Right, 1=Top, 2=Left, 3=Bottom
        public bool clockwise = false;           // 시계 방향으로 채울지 여부

        [Header("Text")]
        public bool autoResizeText = true;
        public float textFontMin = 18f;
        public float textFontMax = 42f;

        private void Awake()
        {
            // Inspector에서 드래그해도 되고, 없으면 자동 탐색
            if (health == null)
                health = FindAnyObjectByType<Health>();

            if (healthText == null)
                healthText = GetComponentInChildren<TMP_Text>(true);

            if (dividersUI == null)
                dividersUI = GetComponentInChildren<CircularDividersUI>(true);

            SetupCircleImage();
            SetupHealthText();
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
            if (healthCircle == null) return;

            // 체력 비율 계산 (0.0 ~ 1.0)
            float ratio = Mathf.Clamp01((float)current / max);

            // 원형 체력 바 fillAmount 설정
            healthCircle.fillAmount = ratio;

            // 체력 비율에 따라 색상 변경
            healthCircle.color = GetBarColor(ratio);

            // 체력 텍스트 표시 (현재/최대)
            if (healthText != null)
                healthText.text = $"{current} / {max}";

            // Divider UI도 현재 HP / MaxHP 기준으로 동기화
            UpdateDividerVisibility(current, max);
        }

        private void SetupCircleImage()
        {
            if (healthCircle == null) return;

            // 원형 체력 게이지로 동작하도록 Image 설정
            healthCircle.type = Image.Type.Filled;
            healthCircle.fillMethod = Image.FillMethod.Radial360;
            healthCircle.fillOrigin = fillOrigin;
            healthCircle.fillClockwise = clockwise;
            healthCircle.preserveAspect = true;
        }

        private void SetupHealthText()
        {
            if (healthText == null) return;

            // 텍스트가 원 안에 들어오도록 자동 크기 조절
            healthText.enableAutoSizing = autoResizeText;
            healthText.fontSizeMin = textFontMin;
            healthText.fontSizeMax = textFontMax;
            healthText.alignment = TextAlignmentOptions.Center;
            healthText.overflowMode = TextOverflowModes.Overflow;
        }

        /// <summary>
        /// 체력 비율에 따라 원형 바 색상을 결정합니다.
        /// - ratio > yellowThreshold: 초록색
        /// - ratio > redThreshold: 노란색
        /// - ratio ≤ redThreshold: 빨간색
        /// Inspector에서 yellowThreshold, redThreshold를 조정할 수 있습니다.
        /// </summary>
        private Color GetBarColor(float ratio)
        {
            float safeThreshold = Mathf.Clamp01(yellowThreshold);
            float dangerThreshold = Mathf.Clamp01(redThreshold);

            // threshold가 잘못 입력되어도 최소한의 안전장치
            if (safeThreshold < dangerThreshold)
                safeThreshold = dangerThreshold;

            if (ratio > safeThreshold)
                return Color.green;
            else if (ratio > dangerThreshold)
                return Color.yellow;
            else
                return Color.red;
        }

        private void UpdateDividerVisibility(int current, int max)
        {
            if (dividersUI == null) return;

            // HP 변화와 MaxHP 변화를 모두 반영해서
            // Divider 재생성 / 표시 개수 갱신을 한 번에 처리
            dividersUI.Sync(current, max);
        }
    }
}