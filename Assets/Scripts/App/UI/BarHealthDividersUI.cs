using UnityEngine;
using UnityEngine.UI;

namespace Dungeon.UI
{
#if UNITY_EDITOR
    using UnityEditor;
#endif

    /// <summary>
    /// 막대형 HP UI 위에 HP 구간선을 생성합니다.
    /// - 10HP마다 얇은 검은 선
    /// - 50HP마다 굵은 검은 선
    /// 
    /// 이 스크립트는 HealthFill과 같은 크기의 RectTransform에 붙이는 것을 권장합니다.
    /// 생성된 선들은 HP Fill 위에 놓여서 초록/노랑/빨강 영역에서는 보이고,
    /// 체력이 줄어든 검은 배경 영역에서는 자연스럽게 잘 보이지 않게 됩니다.
    /// </summary>
    [ExecuteAlways]
    public class BarHealthDividersUI : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Health를 찾지 못했거나 에디터에서 미리보기할 때 사용할 최대 체력입니다.")]
        [Min(1)] public int manualMaxHP = 100;

        [Tooltip("얇은 선을 몇 HP마다 넣을지 정합니다.")]
        [Min(1)] public int thinStepHP = 10;

        [Tooltip("굵은 선을 몇 HP마다 넣을지 정합니다.")]
        [Min(1)] public int thickStepHP = 50;

        [Header("Look")]
        public Color lineColor = Color.black;
        public Sprite lineSprite;
        [Min(1f)] public float thinLineWidth = 4f;
        [Min(1f)] public float thickLineWidth = 8f;

        [Tooltip("선 위아래 여백입니다. 0이면 막대 높이를 꽉 채웁니다.")]
        [Min(0f)] public float verticalPadding = 0f;

        [Tooltip("0HP와 MaxHP 위치의 바깥 경계선은 배경 막대가 담당하므로 보통 생성하지 않습니다.")]
        public bool includeOuterEdges = false;

        private RectTransform rectTransform;
        private int lastMaxHP = -1;
        private int lastThinStep = -1;
        private int lastThickStep = -1;
        private bool rebuildQueued;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            rectTransform = GetComponent<RectTransform>();
            RequestRebuild();
        }

        private void OnValidate()
        {
            rectTransform = GetComponent<RectTransform>();
            RequestRebuild();
        }

        /// <summary>
        /// BarHealthUI에서 MaxHP가 바뀔 때 호출됩니다.
        /// MaxHP가 그대로라면 불필요한 재생성을 하지 않습니다.
        /// </summary>
        public void Sync(int maxHP)
        {
            maxHP = Mathf.Max(1, maxHP);

            if (maxHP == lastMaxHP && thinStepHP == lastThinStep && thickStepHP == lastThickStep)
                return;

            manualMaxHP = maxHP;
            Rebuild(maxHP);
        }

        /// <summary>
        /// 에디터 Inspector 변경 시점에는 즉시 DestroyImmediate를 반복 호출하지 않도록 재생성을 예약합니다.
        /// </summary>
        private void RequestRebuild()
        {
#if UNITY_EDITOR
            if (!rebuildQueued)
            {
                rebuildQueued = true;
                EditorApplication.delayCall += DelayedRebuild;
            }
#else
            Rebuild(manualMaxHP);
#endif
        }

#if UNITY_EDITOR
        private void DelayedRebuild()
        {
            EditorApplication.delayCall -= DelayedRebuild;
            rebuildQueued = false;

            if (this == null) return;
            Rebuild(manualMaxHP);
        }
#endif

        /// <summary>
        /// 현재 MaxHP와 step 설정에 맞춰 모든 구간선을 다시 생성합니다.
        /// </summary>
        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            Rebuild(manualMaxHP);
        }

        public void Rebuild(int maxHP)
        {
            rectTransform = GetComponent<RectTransform>();
            maxHP = Mathf.Max(1, maxHP);
            thinStepHP = Mathf.Max(1, thinStepHP);
            thickStepHP = Mathf.Max(1, thickStepHP);

            ClearChildrenSafe();

            int start = includeOuterEdges ? 0 : thinStepHP;
            int end = includeOuterEdges ? maxHP : maxHP - 1;

            for (int hp = start; hp <= end; hp += thinStepHP)
            {
                if (!includeOuterEdges && (hp <= 0 || hp >= maxHP))
                    continue;

                bool isThick = thickStepHP > 0 && hp % thickStepHP == 0;
                float ratio = Mathf.Clamp01((float)hp / maxHP);
                CreateDivider($"HPDivider_{hp:000}", ratio, isThick);
            }

            lastMaxHP = maxHP;
            lastThinStep = thinStepHP;
            lastThickStep = thickStepHP;
        }

        /// <summary>
        /// ratio 위치에 세로선을 하나 생성합니다.
        /// anchor를 ratio에 직접 고정하므로 부모 RectTransform 크기가 바뀌어도 위치가 유지됩니다.
        /// </summary>
        private void CreateDivider(string objName, float ratio, bool thick)
        {
            var go = new GameObject(objName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(ratio, 0f);
            rt.anchorMax = new Vector2(ratio, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(thick ? thickLineWidth : thinLineWidth, -verticalPadding * 2f);

            var image = go.GetComponent<Image>();
            image.sprite = lineSprite;
            image.color = lineColor;
            image.raycastTarget = false;
        }

        /// <summary>
        /// 이전에 자동 생성한 Divider 자식들을 삭제합니다.
        /// 이 오브젝트 아래에는 수동으로 다른 UI를 넣지 않는 것을 권장합니다.
        /// </summary>
        private void ClearChildrenSafe()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;

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
