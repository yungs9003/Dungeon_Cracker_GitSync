using System.Collections;
using System.Reflection;
using UnityEngine;
using Dungeon.Manager;
using Dungeon.Player;
using Dungeon.UI;

namespace Dungeon.UI
{
    [RequireComponent(typeof(Collider2D))]
    public class PortalNextMap2D : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private MapSequenceManager2D sequenceManager;
        [SerializeField] private ScreenFader fader;

        [Header("Player Check")]
        [SerializeField] private string playerTag = "Player";
        [Tooltip("플레이어 중심이 포탈 중심으로부터 이 거리 이내에 들어오면 발동합니다.")]
        [SerializeField] private float reachRadius = 0.25f;

        [Header("Fade")]
        [SerializeField] private float fadeOutSeconds = 0.35f;

        private bool triggered;

        private void Reset()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;

            if (sequenceManager == null)
                sequenceManager = FindFirstObjectByType<MapSequenceManager2D>();

            if (fader == null)
                fader = FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (triggered) return;
            if (!other.CompareTag(playerTag)) return;

            PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
            if (player == null) return;

            if (!IsPlayerCenterInside(player.transform)) return;

            triggered = true;
            StartCoroutine(CoMoveNext(player));
        }

        private bool IsPlayerCenterInside(Transform playerTransform)
        {
            Vector2 playerCenter = playerTransform.position;
            Vector2 portalCenter = transform.position;
            return Vector2.Distance(playerCenter, portalCenter) <= reachRadius;
        }

        private IEnumerator CoMoveNext(PlayerController2D player)
        {
            SetPlayerControl(player, false, true);

            if (fader == null)
                fader = FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);

            if (fader != null)
                yield return fader.FadeOut(fadeOutSeconds);

            if (sequenceManager == null)
                sequenceManager = FindFirstObjectByType<MapSequenceManager2D>();

            if (sequenceManager != null)
                sequenceManager.LoadNextMapOrFinish(player);
            else
            {
                Debug.LogError("[PortalNextMap2D] MapSequenceManager를 찾지 못했습니다.");
                SetPlayerControl(player, true, true);
                triggered = false;
            }
        }

        private void SetPlayerControl(Component playerController, bool enabled, bool stopImmediately)
        {
            if (playerController == null) return;

            MethodInfo method = playerController.GetType().GetMethod(
                "SetControl",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(bool), typeof(bool) },
                null
            );

            if (method != null)
                method.Invoke(playerController, new object[] { enabled, stopImmediately });
        }
    }
}
