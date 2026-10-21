using Dungeon.Core;
using Dungeon.Traps;
using Dungeon.Map;
using UnityEngine;

namespace Dungeon.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(PlayerEffectLock2D))]
    public sealed class ForcedMovementReceiver2D : MonoBehaviour
    {
        private readonly RaycastHit2D[] castResults = new RaycastHit2D[128];
        private Rigidbody2D body;
        private PlayerEffectLock2D gate;
        private Vector2 footprintSize;
        private Vector2 footprintOffset;
        private const float WallSkin = 0.02f;

        public bool IsMoving => gate != null && gate.IsProtected;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            gate = GetComponent<PlayerEffectLock2D>();
        }

        public bool TryBegin(Object owner, Vector2 requestedDestination, LayerMask obstacleMask,
            out Vector2 destination)
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (gate == null) gate = GetComponent<PlayerEffectLock2D>();
            destination = body.position;
            var controller = GetComponent<PlayerController2D>();
            if (!body.simulated || IsMoving || CliffTrap2D.IsPlayerFalling(body)) return false;
            if (controller != null && (!controller.isActiveAndEnabled || !controller.CanControl)) return false;
            var health = GetComponent<Health>();
            if (health != null && health.currentHP <= 0) return false;

            CaptureFootprint();
            if (!TryLimitByWalls(body.position, requestedDestination, obstacleMask, out destination)) return false;
            // 이미 당기기 도착점에 있거나 벽에 막혀도 팔 동작과 보호 사이클은 시작합니다.
            if (!gate.TryAcquire(owner)) return false;
            GetComponent<KnockbackReceiver2D>()?.CancelKnockback();
            return true;
        }

        public bool MoveTo(Object owner, Vector2 requestedPosition, LayerMask obstacleMask)
        {
            if (!gate.IsOwnedBy(owner)) return false;
            if (!TryLimitByWalls(body.position, requestedPosition, obstacleMask, out Vector2 position)) return false;
            body.position = position;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            return (position - requestedPosition).sqrMagnitude < 0.000001f;
        }

        public void End(Object owner) => gate.Release(owner);

        private void CaptureFootprint()
        {
            bool found = false;
            Bounds bounds = default;
            foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
            {
                if (!col.enabled || col.isTrigger || col.attachedRigidbody != body) continue;
                if (!found) { bounds = col.bounds; found = true; }
                else bounds.Encapsulate(col.bounds);
            }
            footprintSize = found ? (Vector2)bounds.size : new Vector2(0.6f, 0.6f);
            footprintSize = Vector2.Max(footprintSize, Vector2.one * 0.01f);
            footprintOffset = found ? (Vector2)bounds.center - body.position : Vector2.zero;
        }

        private bool TryLimitByWalls(Vector2 from, Vector2 to, LayerMask obstacleMask, out Vector2 limited)
        {
            limited = to;
            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.000001f) return true;

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(obstacleMask.value | LayerMask.GetMask(MapArea2D.BoundaryLayerName));
            filter.useTriggers = false;
            int count = Physics2D.BoxCast(from + footprintOffset, footprintSize, 0f,
                delta / distance, filter, castResults, distance + WallSkin);
            if (count == castResults.Length) { limited = from; return false; }
            float allowed = distance;
            for (int i = 0; i < count; i++)
            {
                var col = castResults[i].collider;
                if (col == null || col.attachedRigidbody == body || col.isTrigger) continue;
                // 구르는 돌 등 다른 함정의 본체는 통과하되, 벽형 함정의 벽은 지형으로 취급합니다.
                string code = TrapDamageEvent.ResolveTrapCode(col.gameObject);
                if (col.GetComponentInParent<WallForceTrap2D>() == null &&
                    (code.StartsWith("10", global::System.StringComparison.Ordinal) ||
                     col.GetComponentInParent<IceCrystalObstacle2D>() != null ||
                     col.GetComponentInParent<RollingBallTrap2D>() != null)) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0f, castResults[i].distance - WallSkin));
            }
            limited = from + delta.normalized * allowed;
            var area = MapArea2D.ForBody(body);
            if (area != null) limited = area.LimitMovement(body, from, limited, false);
            return true;
        }
    }
}
