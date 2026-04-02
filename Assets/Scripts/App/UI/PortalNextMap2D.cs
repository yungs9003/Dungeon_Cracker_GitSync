using System.Collections;
using UnityEngine;
using Dungeon.Player;
using Dungeon.Manager;

namespace Dungeon.UI
{
    public class PortalNextMap2D : MonoBehaviour
    {
        [Header("Detect")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private Collider2D enterZone;

        private bool triggered;

        private void Awake()
        {
            if (enterZone == null)
                enterZone = GetComponent<Collider2D>();
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (triggered) return;
            if (enterZone == null) return;

            var pc = other.GetComponentInParent<PlayerController2D>();
            if (pc == null) return;
            if (!pc.CompareTag(playerTag)) return;

            bool inside = IsPlayerCenterInsideZone(pc, enterZone);
            Debug.Log($"[Portal2D] center={pc.transform.position} inside={inside}");

            if (!inside) return;

            triggered = true;
            StartCoroutine(CoNextMap(pc));
        }

        private bool IsPlayerCenterInsideZone(PlayerController2D pc, Collider2D zone)
        {
            Vector2 playerCenter = pc.transform.position;
            return zone.OverlapPoint(playerCenter);
        }

        private IEnumerator CoNextMap(PlayerController2D pc)
        {
            Debug.Log("[Portal2D] CoNextMap start");

            pc.SetControl(false, stopImmediately: true);

            yield return null;

            if (MapSequenceManager2D.Instance == null)
            {
                Debug.LogError("[Portal2D] MapSequenceManager2D.Instance is null");
                pc.SetControl(true, stopImmediately: true);
                triggered = false;
                yield break;
            }

            MapSequenceManager2D.Instance.LoadNextMapOrFinish(pc);
        }
    }
}