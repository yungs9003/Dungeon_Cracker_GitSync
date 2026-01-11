using System.Collections;
using UnityEngine;

public class ScreenFader : MonoBehaviour
{
    [SerializeField] private CanvasGroup cg;

    private void Awake()
    {
        if (cg == null) cg = GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
    }

    public IEnumerator FadeOut(float seconds)
    {
        cg.blocksRaycasts = true;

        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(t / seconds);
            yield return null;
        }
        cg.alpha = 1f;
    }
}
