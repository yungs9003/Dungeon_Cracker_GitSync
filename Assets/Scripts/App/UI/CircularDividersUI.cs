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
        [SerializeField] private BarHealthUI healthUI; // 연결된 체력 UI
        [SerializeField] private Health health;        // 실제 체력 데이터

        [Header("Divider Rule")]
        [Min(1)]
        [SerializeField] private int hpPerDivider = 10; // 몇 HP마다 Divider 한 칸으로 볼지

        [Tooltip("health / healthUI가 없을 때만 manualMaxHP 사용")]
        [Min(1)]
        [SerializeField] private int manualMaxHP = 100; // fallback 최대 체력

        [Header("Top Divider")]
        [SerializeField] private bool alwaysShowTopDivider = true; // 12시 방향 Divider를 항상 표시할지
        [SerializeField] private float topDividerAngleDeg = 0f;    // 고정 Divider 각도

        [Header("Look")]
        [SerializeField] private Sprite lineSprite;       // Divider에 사용할 스프라이트
        [SerializeField] private Color lineColor = Color.black;
        [SerializeField] private float lineWidth = 4f;
        [SerializeField] private float lineLength = 28f;
        [SerializeField] private float startAngleDeg = 0f; // 동적 Divider 시작 각도

        // OnValidate에서 직접 Rebuild하지 않고 예약하기 위한 플래그
        private bool pendingRebuild;

        // 12시 방향에 고정으로 남아있는 Divider
        private Image topDividerImage;

        // 체력 변화에 따라 보였다/사라지는 Divider 목록
        private readonly List<Image> dynamicDividerImages = new();

        // 마지막으로 생성에 사용했던 값들을 저장
        // 값이 달라지면 Rebuild가 필요함
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

        /// <summary>
        /// healthUI / health 참조를 자동으로 연결합니다.
        /// Inspector에서 직접 넣어도 되고, 없으면 부모/씬에서 탐색합니다.
        /// </summary>
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

        /// <summary>
        /// Rebuild를 예약합니다.
        /// 에디터에서는 OnValidate 중 직접 DestroyImmediate를 피하기 위해 delayCall 사용.
        /// </summary>
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

        /// <summary>
        /// Divider들을 다시 생성합니다.
        /// - 현재 MaxHP
        /// - hpPerDivider
        /// 를 기준으로 동적 Divider 개수를 계산합니다.
        /// </summary>
        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            AutoBind();

            int maxHP = GetResolvedMaxHP();
            int dynamicDividerCount = CalculateDynamicDividerCount(maxHP, hpPerDivider);

            // 기존 Divider 제거
            ClearChildrenSafe();
            dynamicDividerImages.Clear();
            topDividerImage = null;

            // 12시 고정 Divider 생성
            if (alwaysShowTopDivider)
                topDividerImage = CreateDivider("Divider_TOP", topDividerAngleDeg);

            // 동적 Divider 생성
            if (dynamicDividerCount > 0)
            {
                // 구간이 N개면 Divider는 N-1개
                // 즉 Divider는 각 구간 사이 경계에 배치됨
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

            // 생성 직후 현재 체력 상태 반영
            int currentHP = GetResolvedCurrentHP();
            Sync(currentHP, maxHP);
        }

        /// <summary>
        /// 현재 HP / MaxHP를 받아 Divider 표시 상태를 갱신합니다.
        /// MaxHP나 hpPerDivider가 바뀌면 자동으로 Rebuild합니다.
        /// </summary>
        public void Sync(int currentHP, int maxHP)
        {
            maxHP = Mathf.Max(1, maxHP);
            currentHP = Mathf.Clamp(currentHP, 0, maxHP);

            // Divider 개수 자체가 달라져야 하면 재생성
            if (NeedsRebuild(maxHP))
            {
                Rebuild();
                return;
            }

            // 현재 체력 구간에 맞게 동적 Divider 몇 개를 보여줄지 계산
            int visibleDynamicCount = CalculateVisibleDynamicDividerCount(currentHP, maxHP, hpPerDivider);

            for (int i = 0; i < dynamicDividerImages.Count; i++)
            {
                if (dynamicDividerImages[i] != null)
                    dynamicDividerImages[i].enabled = i < visibleDynamicCount;
            }

            // 고정 Divider는 항상 유지
            if (topDividerImage != null)
                topDividerImage.enabled = alwaysShowTopDivider;
        }

        /// <summary>
        /// Divider 재생성이 필요한지 검사합니다.
        /// </summary>
        private bool NeedsRebuild(int maxHP)
        {
            return lastBuiltMaxHP != maxHP || lastBuiltHpPerDivider != hpPerDivider;
        }

        /// <summary>
        /// 사용할 MaxHP를 결정합니다.
        /// 우선순위:
        /// 1. health.maxHP
        /// 2. healthUI.health.maxHP
        /// 3. manualMaxHP
        /// </summary>
        private int GetResolvedMaxHP()
        {
            if (health != null)
                return Mathf.Max(1, health.maxHP);

            if (healthUI != null && healthUI.health != null)
                return Mathf.Max(1, healthUI.health.maxHP);

            return Mathf.Max(1, manualMaxHP);
        }

        /// <summary>
        /// 사용할 현재 HP를 결정합니다.
        /// </summary>
        private int GetResolvedCurrentHP()
        {
            if (health != null)
                return Mathf.Max(0, health.currentHP);

            if (healthUI != null && healthUI.health != null)
                return Mathf.Max(0, healthUI.health.currentHP);

            return GetResolvedMaxHP();
        }

        /// <summary>
        /// MaxHP와 hpPerDivider를 기준으로
        /// "동적 Divider 개수"를 계산합니다.
        /// 
        /// 예:
        /// maxHP=100, hpPerDivider=20
        /// -> 구간 수 5개
        /// -> Divider 수 4개
        /// </summary>
        private int CalculateDynamicDividerCount(int maxHP, int hpStep)
        {
            int stageCount = Mathf.CeilToInt((float)maxHP / hpStep);
            return Mathf.Max(0, stageCount - 1);
        }

        /// <summary>
        /// 현재 HP 기준으로 몇 개의 동적 Divider를 보여줄지 계산합니다.
        /// 
        /// 예:
        /// maxHP=100, hpPerDivider=20
        /// 구간:
        /// 81~100, 61~80, 41~60, 21~40, 1~20
        /// 
        /// currentHP가 75라면 현재 구간은 4번째 구간이므로
        /// 보이는 Divider는 3개가 됩니다.
        /// </summary>
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

        /// <summary>
        /// Divider 하나를 생성합니다.
        /// </summary>
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

        /// <summary>
        /// 기존 자식 Divider들을 안전하게 삭제합니다.
        /// </summary>
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