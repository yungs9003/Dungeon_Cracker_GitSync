using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class CircularDividersUI : MonoBehaviour
{
    [Header("Link (optional)")]
    [SerializeField] private CircularHealthUI healthUI; // segmentsPerStage 가져오려면 연결

    [Header("Look")]
    [SerializeField] private Sprite lineSprite;         // Square 같은 1x1 스프라이트
    [SerializeField] private Color lineColor = Color.white;
    [SerializeField] private float lineWidth = 2f;
    [SerializeField] private float lineLength = 300f;   // 링 지름보다 크게
    [SerializeField] private float startAngleDeg = 0f;  // 안 맞으면 여기만 조절(+90 등)

    private void OnEnable() => Rebuild();
    private void OnValidate() => Rebuild();

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        int div = healthUI != null ? Mathf.Max(1, healthUI.segmentsPerStage) : 5;

        // 기존 라인 삭제
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(transform.GetChild(i).gameObject);
            else Destroy(transform.GetChild(i).gameObject);
#else
            Destroy(transform.GetChild(i).gameObject);
#endif
        }

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
}
