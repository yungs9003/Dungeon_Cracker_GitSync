using System.Collections;
using UnityEngine;
using Dungeon.Player;
using Dungeon.Manager;
using Dungeon.UI;

namespace Dungeon.Checkpoints
{
public class CheckpointExit2D : MonoBehaviour
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

        if (fader == null)
            fader = FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (triggered) return;

        var pc = other.GetComponentInParent<PlayerController2D>();
        if (pc == null) return;
        if (!pc.CompareTag(playerTag)) return;

        Vector2 playerCenter = other.bounds.center;
        float dist = Vector2.Distance(playerCenter, portalCenter.position);
        if (dist > reachRadius) return;

        triggered = true;
        StartCoroutine(CoExitCheckpoint(pc));
    }

    private IEnumerator CoExitCheckpoint(PlayerController2D pc)
    {
        pc.SetControl(false, stopImmediately: true);

        if (fader != null) yield return fader.FadeOut(fadeOutSeconds);
        else yield return new WaitForSeconds(fadeOutSeconds);

        if (SaveManager.Instance == null || !SaveManager.Instance.ExitCheckpointToSavedNextMap())
        {
            Debug.LogWarning("[CheckpointExit2D] Failed to exit checkpoint. SaveManager or checkpoint data missing.");

            if (pc != null)
                pc.SetControl(true, stopImmediately: true);

            triggered = false;
        }
    }
}
}
