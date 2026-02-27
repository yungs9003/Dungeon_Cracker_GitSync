using UnityEngine;
using UnityEngine.UI;

namespace Dungeon.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class CircularDividersUI : MonoBehaviour
{
    [Header("Link (optional)")]
    [SerializeField] private CircularHealthUI healthUI; // segmentsPerStage 가져오려면 연결

    [Header("Look")]
    [SerializeField] private Sprite lineSprite;
    [SerializeField] private Color lineColor = Color.white;
    [SerializeField] private float lineWidth = 2f;
    [SerializeField] private float lineLength = 300f;
    [SerializeField] private float startAngleDeg = 0f;

    // OnValidate에서 직접 Rebuild하지 않도록 예약 플래그
    private bool pendingRebuild;

    private void OnEnable()
    {
        RequestRebuild();
    }

    private void OnValidate()
    {
        // ✅ 여기서 DestroyImmediate 하면 에러남 → 예약만 한다
        RequestRebuild();
    }

    private void RequestRebuild()
    {
#if UNITY_EDITOR
        // 에디터(플레이 X 또는 실행 중에도 인스펙터 변경 등으로 OnValidate가 호출될 수 있음)
        if (!pendingRebuild)
        {
            pendingRebuild = true;
            EditorApplication.delayCall += DelayedRebuild;
        }
#else
        // 빌드에서는 그냥 즉시
        Rebuild();
#endif
    }

#if UNITY_EDITOR
    private void DelayedRebuild()
    {
        EditorApplication.delayCall -= DelayedRebuild;
        pendingRebuild = false;

        // 오브젝트가 삭제된 상태면 중단
        if (this == null) return;

        Rebuild();
    }
#endif

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        int div = healthUI != null ? Mathf.Max(1, healthUI.segmentsPerStage) : 5;

        // 기존 라인 삭제
        ClearChildrenSafe();

        // 새로 생성
        for (int i = 0; i < div; i++)
        {
            var go = new GameObject($"Divider_{i:00}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(lineWidth, lineLength);
            rt.localRotation = Quaternion.Euler(0, 0, startAngleDeg + (360f / div) * i);

            var img = go.GetComponent<Image>();
            img.sprite = lineSprite;
            img.color = lineColor;
            img.raycastTarget = false;
        }
    }

    private void ClearChildrenSafe()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;

#if UNITY_EDITOR
            // ✅ 플레이 중이면 DestroyImmediate 절대 금지
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child); // ✅ 에디터에서 즉시 삭제 OK (단, OnValidate 밖에서 실행됨)
#else
            Destroy(child);
#endif
        }
    }
}
