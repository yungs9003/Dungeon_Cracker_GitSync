using System.Collections;
using UnityEngine;

namespace Dungeon.UI;

public class PortalNextMap : MonoBehaviour
{
    [Header("Detect")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Transform portalCenter;
    [SerializeField] private float reachRadius = 0.25f;

    [Header("Fade")]
    [SerializeField] private ScreenFader fader;
    [SerializeField] private float fadeOutSeconds = 1.0f;

    private bool triggered;

    private void Awake()
    {
        if (portalCenter == null) portalCenter = transform;

        // ✅ 인스펙터에 넣는 걸 권장, 없으면 찾기
        if (fader == null)
            fader = FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);
    }

    private void OnTriggerStay(Collider other)
    {
        if (triggered) return;

        var pc = other.GetComponentInParent<PlayerController>();
        if (pc == null) return;
        if (!pc.CompareTag(playerTag)) return;

        Vector3 playerCenter = other.bounds.center;
        float dist = Vector3.Distance(playerCenter, portalCenter.position);
        if (dist > reachRadius) return;

        triggered = true;
        StartCoroutine(CoNextMap(pc));
    }

    private IEnumerator CoNextMap(PlayerController pc)
    {
        pc.SetControl(false, stopImmediately: true);

        if (fader != null) yield return fader.FadeOut(fadeOutSeconds);
        else yield return new WaitForSeconds(fadeOutSeconds);

        // ✅ 다음 맵 로드(조작 해제 + FadeIn은 매니저가 처리)
        MapSequenceManager.Instance.LoadNextMapOrFinish(pc);

        // 여기서 triggered를 false로 돌려도,
        // 다음 맵에서 새 포탈이 또 잡히면 다시 동작함
    }
}
