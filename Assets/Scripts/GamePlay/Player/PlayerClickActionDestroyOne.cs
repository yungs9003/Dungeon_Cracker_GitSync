using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Dungeon.Core;
using Dungeon.Traps;

namespace Dungeon.Player
{
    public class PlayerClickActionDestroyOne : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ForwardZoneCollector zone;
        [SerializeField] private Transform 기준점;

        [Header("Optional click rate limit")]
        [SerializeField] private float clickCooldown = 0.08f;
        private float nextTime;

        [Header("Trap Destroy Delay")]
        [SerializeField] private float trapDestroyDelay = 0.2f;
        private bool isTrapDestroyPending;

        private Destructible currentHighlighted;
        private TrapZoneHighlight currentHighlightComp;

        private void Awake()
        {
            if (기준점 == null) 기준점 = transform;

            if (zone == null)
                zone = GetComponentInChildren<ForwardZoneCollector>();
        }

        private void Update()
        {
            RefreshTrapHighlight();
        }

        private void OnDisable()
        {
            ClearCurrentHighlight();
            isTrapDestroyPending = false;
            StopAllCoroutines();
        }

        private void OnDestroy()
        {
            ClearCurrentHighlight();
        }

        public void OnAction(InputValue value)
        {
            if (!value.isPressed) return;
            HandleAction();
        }

        public void OnAction(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            HandleAction();
        }

        private void HandleAction()
        {
            if (zone == null) return;

            if (clickCooldown > 0f && Time.time < nextTime) return;
            nextTime = Time.time + clickCooldown;

            zone.projectiles.RemoveAll(p => p == null);
            zone.destructibles.RemoveAll(d => d == null);

            // 1순위: 투사체 즉시 제거
            if (TryDestroyNearestProjectile()) return;

            // 2순위: 함정은 0.2초 딜레이 후 제거
            TryDestroyHighlightedDestructibleDelayed();
        }

        private void RefreshTrapHighlight()
        {
            if (zone == null)
            {
                ClearCurrentHighlight();
                return;
            }

            zone.destructibles.RemoveAll(d => d == null);

            Destructible nearest = GetNearestDestructible();

            if (nearest == currentHighlighted) return;

            ClearCurrentHighlight();

            if (nearest != null)
            {
                currentHighlighted = nearest;
                currentHighlightComp = nearest.GetComponent<TrapZoneHighlight>();

                if (currentHighlightComp != null)
                    currentHighlightComp.SetHighlighted(true);
            }
        }

        private void ClearCurrentHighlight()
        {
            if (currentHighlightComp != null)
                currentHighlightComp.SetHighlighted(false);

            currentHighlighted = null;
            currentHighlightComp = null;
        }

        private Destructible GetNearestDestructible()
        {
            if (zone.destructibles == null || zone.destructibles.Count == 0)
                return null;

            Destructible best = null;
            float bestDist = float.PositiveInfinity;

            foreach (var d in zone.destructibles)
            {
                if (d == null) continue;

                float dist = ((Vector2)d.transform.position - (Vector2)기준점.position).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = d;
                }
            }

            return best;
        }

        private bool TryDestroyNearestProjectile()
        {
            if (zone.projectiles == null || zone.projectiles.Count == 0)
                return false;

            MonoBehaviour best = null;
            float bestDist = float.PositiveInfinity;

            foreach (var mb in zone.projectiles)
            {
                if (mb == null) continue;

                float d = ((Vector2)mb.transform.position - (Vector2)기준점.position).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = mb;
                }
            }

            if (best == null) return false;

            zone.projectiles.Remove(best);

            if (best is IPlayerDestroyableProjectile p)
            {
                p.DestroyByPlayer();
                return true;
            }

            return false;
        }

        private bool TryDestroyHighlightedDestructibleDelayed()
        {
            if (currentHighlighted == null) return false;
            if (isTrapDestroyPending) return false;

            Destructible target = currentHighlighted;

            zone.destructibles.Remove(target);
            ClearCurrentHighlight();

            StartCoroutine(DestroyTrapAfterDelay(target, trapDestroyDelay));
            return true;
        }

        private IEnumerator DestroyTrapAfterDelay(Destructible target, float delay)
        {
            isTrapDestroyPending = true;

            yield return new WaitForSeconds(delay);

            if (target != null)
                target.ExecuteDestruction();

            isTrapDestroyPending = false;
        }
    }
}