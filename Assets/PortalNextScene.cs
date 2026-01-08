using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PortalNextScene : MonoBehaviour
{
    [Header("Detect")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Transform portalCenter;     // PortalCenter 넣기
    [SerializeField] private float reachRadius = 0.25f;  // 도달 판정 반경(조절)

    [Header("Fade & Load")]
    [SerializeField] private ScreenFader fader;
    [SerializeField] private float fadeOutSeconds = 2f;

    [Header("Scene")]
    [SerializeField] private string nextSceneName = "";

    private bool triggered;

    private void Awake()
    {
        if (portalCenter == null) portalCenter = transform; // 없으면 자기 중심
        if (fader == null) fader = FindFirstObjectByType<ScreenFader>();
    }

    private void OnTriggerStay(Collider other)
    {
        if (triggered) return;

        // ✅ 자식 콜라이더 문제 방지: 부모에서 PlayerController 찾기
        var pc = other.GetComponentInParent<PlayerController>();
        if (pc == null) return;
        if (!pc.CompareTag(playerTag)) return;

        // ✅ “플레이어 콜라이더 중심” = bounds.center
        Vector3 playerCenter = other.bounds.center;

        float dist = Vector3.Distance(playerCenter, portalCenter.position);
        if (dist > reachRadius) return;

        triggered = true;
        StartCoroutine(CoTransition(pc));
    }

    private IEnumerator CoTransition(PlayerController pc)
    {
        pc.SetControl(false, stopImmediately: true);

        if (fader != null) yield return fader.FadeOut(fadeOutSeconds);
        else yield return new WaitForSeconds(fadeOutSeconds);

        if (!string.IsNullOrEmpty(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

#if UNITY_EDITOR
    // Scene 뷰에서 반경 확인용(선택)
    private void OnDrawGizmosSelected()
    {
        if (portalCenter == null) return;
        Gizmos.DrawWireSphere(portalCenter.position, reachRadius);
    }
#endif
}
