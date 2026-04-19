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
        public Image healthCircle;
        public TMP_Text healthText;
        public CircularDividersUI dividersUI;

        [Header("Thresholds")]
        [Range(0f, 1f)] public float yellowThreshold = 0.5f;
        [Range(0f, 1f)] public float redThreshold = 0.15f;

        [Header("Colors")]
        public Color greenColor  = new Color(0.25f, 1.00f, 0.25f, 1f);
        public Color yellowColor = new Color(1.00f, 0.88f, 0.25f, 1f);
        public Color redColor    = new Color(1.00f, 0.45f, 0.45f, 1f);

        [Header("Circle")]
        [Range(0, 3)] public int fillOrigin = 2;
        public bool clockwise = false;

        [Header("Text")]
        public bool autoResizeText = true;
        public float textFontMin = 18f;
        public float textFontMax = 42f;

        private void Awake()
        {
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

            float ratio = Mathf.Clamp01((float)current / max);

            healthCircle.fillAmount = ratio;
            healthCircle.color = GetBarColor(ratio);

            if (healthText != null)
                healthText.text = $"{current} / {max}";

            UpdateDividerVisibility(current, max);
        }

        private void SetupCircleImage()
        {
            if (healthCircle == null) return;

            healthCircle.type = Image.Type.Filled;
            healthCircle.fillMethod = Image.FillMethod.Radial360;
            healthCircle.fillOrigin = fillOrigin;
            healthCircle.fillClockwise = clockwise;
            healthCircle.preserveAspect = true;
        }

        private void SetupHealthText()
        {
            if (healthText == null) return;

            healthText.enableAutoSizing = autoResizeText;
            healthText.fontSizeMin = textFontMin;
            healthText.fontSizeMax = textFontMax;
            healthText.alignment = TextAlignmentOptions.Center;
            healthText.overflowMode = TextOverflowModes.Overflow;
        }

        private Color GetBarColor(float ratio)
        {
            float safeThreshold = Mathf.Clamp01(yellowThreshold);
            float dangerThreshold = Mathf.Clamp01(redThreshold);

            if (safeThreshold < dangerThreshold)
                safeThreshold = dangerThreshold;

            if (ratio > safeThreshold)
                return greenColor;
            else if (ratio > dangerThreshold)
                return yellowColor;
            else
                return redColor;
        }

        private void UpdateDividerVisibility(int current, int max)
        {
            if (dividersUI == null) return;

            // HP 변화와 MaxHP 변화를 모두 반영해서
            // divider 재생성 / 표시 개수 갱신을 한 번에 처리
            dividersUI.Sync(current, max);
        }
    }
}