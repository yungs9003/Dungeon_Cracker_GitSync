using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dungeon.Player;

namespace Dungeon.UI
{
#if UNITY_EDITOR
    using UnityEditor;
#endif

    [ExecuteAlways]
    public class CircularDividersUI : MonoBehaviour
    {
        [Header("Link")]
        [SerializeField] private BarHealthUI healthUI;
        [SerializeField] private Health health;

        [Header("Divider Rule")]
        [Min(1)]
        [SerializeField] private int hpPerDivider = 10;

        [Tooltip("health가 없을 때만 사용하는 fallback 최대체력")]
        [Min(1)]
        [SerializeField] private int manualMaxHP = 100;

        [Header("Top Divider")]
        [SerializeField] private bool alwaysShowTopDivider = true;
        [SerializeField] private float topDividerAngleDeg = 0f; 
        // 스프라이트 기준 상단 방향이 다르면 0 대신 90, -90 등 조정

        [Header("Look")]
        [SerializeField] private Sprite lineSprite;
        [SerializeField] private Color lineColor = Color.black;
        [SerializeField] private float lineWidth = 4f;
        [SerializeField] private float lineLength = 28f;
        [SerializeField] private float startAngleDeg = 0f;

        private bool pendingRebuild;

        private Image topDividerImage;
        private readonly List<Image> dynamicDividerImages = new();

        private int lastBuiltMaxHP = -1;
        private int lastBuiltHpPerDivider = -1;

        public int HpPerDivider => hpPerDivider;

        private void Awake()
        {
            AutoBind();
        }

        private void OnEnable()
        {
            AutoBind();
            RequestRebuild();
        }

        private void OnValidate()
        {
            AutoBind();
            RequestRebuild();
        }

        private void AutoBind()
        {
            if (healthUI == null)
                healthUI = GetComponentInParent<BarHealthUI>();

            if (health == null)
            {
                if (healthUI != null && healthUI.health != null)
                    health = healthUI.health;
                else
                    health = FindAnyObjectByType<Health>();
            }
        }

        private void RequestRebuild()
        {
#if UNITY_EDITOR
            if (!pendingRebuild)
            {
                pendingRebuild = true;
                EditorApplication.delayCall += DelayedRebuild;
            }
#else
            Rebuild();
#endif
        }

#if UNITY_EDITOR
        private void DelayedRebuild()
        {
            EditorApplication.delayCall -= DelayedRebuild;
            pendingRebuild = false;

            if (this == null) return;
            Rebuild();
        }
#endif

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            AutoBind();

            int maxHP = GetResolvedMaxHP();
            int dynamicDividerCount = CalculateDynamicDividerCount(maxHP, hpPerDivider);

            ClearChildrenSafe();
            dynamicDividerImages.Clear();
            topDividerImage = null;

            if (alwaysShowTopDivider)
                topDividerImage = CreateDivider("Divider_TOP", topDividerAngleDeg);

            if (dynamicDividerCount > 0)
            {
                float stepAngle = 360f / (dynamicDividerCount + 1);

                for (int i = 0; i < dynamicDividerCount; i++)
                {
                    float angle = startAngleDeg + stepAngle * (i + 1);
                    var img = CreateDivider($"Divider_{i:00}", angle);
                    dynamicDividerImages.Add(img);
                }
            }

            lastBuiltMaxHP = maxHP;
            lastBuiltHpPerDivider = hpPerDivider;

            int currentHP = GetResolvedCurrentHP();
            Sync(currentHP, maxHP);
        }

        public void Sync(int currentHP, int maxHP)
        {
            maxHP = Mathf.Max(1, maxHP);
            currentHP = Mathf.Clamp(currentHP, 0, maxHP);

            if (NeedsRebuild(maxHP))
            {
                Rebuild();
                return;
            }

            int visibleDynamicCount = CalculateVisibleDynamicDividerCount(currentHP, maxHP, hpPerDivider);

            for (int i = 0; i < dynamicDividerImages.Count; i++)
            {
                if (dynamicDividerImages[i] != null)
                    dynamicDividerImages[i].enabled = i < visibleDynamicCount;
            }

            if (topDividerImage != null)
                topDividerImage.enabled = alwaysShowTopDivider;
        }

        private bool NeedsRebuild(int maxHP)
        {
            return lastBuiltMaxHP != maxHP || lastBuiltHpPerDivider != hpPerDivider;
        }

        private int GetResolvedMaxHP()
        {
            if (health != null)
                return Mathf.Max(1, health.maxHP);

            if (healthUI != null && healthUI.health != null)
                return Mathf.Max(1, healthUI.health.maxHP);

            return Mathf.Max(1, manualMaxHP);
        }

        private int GetResolvedCurrentHP()
        {
            if (health != null)
                return Mathf.Max(0, health.currentHP);

            if (healthUI != null && healthUI.health != null)
                return Mathf.Max(0, healthUI.health.currentHP);

            return GetResolvedMaxHP();
        }

        private int CalculateDynamicDividerCount(int maxHP, int hpStep)
        {
            int stageCount = Mathf.CeilToInt((float)maxHP / hpStep);

            // 구간이 1개면 divider 0개
            // 구간이 5개면 divider 4개
            return Mathf.Max(0, stageCount - 1);
        }

        private int CalculateVisibleDynamicDividerCount(int currentHP, int maxHP, int hpStep)
        {
            int totalDynamic = CalculateDynamicDividerCount(maxHP, hpStep);
            if (totalDynamic <= 0) return 0;
            if (currentHP <= 0) return 0;

            int currentStage = Mathf.CeilToInt((float)currentHP / hpStep);

            // stage 1 -> divider 0개
            // stage 2 -> divider 1개
            // ...
            return Mathf.Clamp(currentStage - 1, 0, totalDynamic);
        }

        private Image CreateDivider(string objName, float zAngle)
        {
            var go = new GameObject(objName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(lineWidth, lineLength);
            rt.localRotation = Quaternion.Euler(0f, 0f, zAngle);

            var img = go.GetComponent<Image>();
            img.sprite = lineSprite;
            img.color = lineColor;
            img.raycastTarget = false;

            return img;
        }

        private void ClearChildrenSafe()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;

#if UNITY_EDITOR
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
#else
                Destroy(child);
#endif
            }
        }
    }
}