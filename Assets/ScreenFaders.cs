using System.Collections;
using UnityEngine;

public class ScreenFader : MonoBehaviour
{
    [SerializeField] private CanvasGroup cg;

    [Header("Disable object when fully clear")]
    [SerializeField] private bool disableWhenClear = true;

    private void Awake()
    {
        if (cg == null) cg = GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;

        if (disableWhenClear) gameObject.SetActive(false);
    }

    public IEnumerator FadeOut(float seconds)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        cg.blocksRaycasts = true;

        float start = cg.alpha;
        float t = 0f;

        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = seconds <= 0f ? 1f : Mathf.Clamp01(t / seconds);
            cg.alpha = Mathf.Lerp(start, 1f, k);
            yield return null;
        }

        cg.alpha = 1f;
        cg.blocksRaycasts = true;
    }

    public IEnumerator FadeIn(float seconds)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        float start = cg.alpha;
        float t = 0f;

        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = seconds <= 0f ? 1f : Mathf.Clamp01(t / seconds);
            cg.alpha = Mathf.Lerp(start, 0f, k);
            yield return null;
        }

        cg.alpha = 0f;
        cg.blocksRaycasts = false;

        if (disableWhenClear)
            gameObject.SetActive(false);
    }
}
