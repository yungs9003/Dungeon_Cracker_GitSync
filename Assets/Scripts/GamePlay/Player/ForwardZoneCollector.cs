using System.Collections.Generic;
using UnityEngine;
using Dungeon.Traps;
using Dungeon.Core;

namespace Dungeon.Player
{
    public class ForwardZoneCollector : MonoBehaviour
    {
        [Header("Direction")]
        [SerializeField] private PlayerMoveDirectionTracker dirTracker;

        [Header("Zone Shape")]
        [SerializeField] private float forwardRadius = 2f;      // 입력 방향 ±60도
        [SerializeField] private float sideBackRadius = 0.8f;   // 나머지 방향
        [SerializeField, Range(0f, 180f)] private float halfAngle = 60f;

        [Header("Detection")]
        [SerializeField] private LayerMask detectionMask = ~0; // 전부 검사, 필요시 제한

        public readonly List<MonoBehaviour> projectiles = new();
        public readonly List<Destructible> destructibles = new();

        private readonly Collider2D[] results = new Collider2D[64];

        private void Awake()
        {
            if (dirTracker == null)
                dirTracker = GetComponentInParent<PlayerMoveDirectionTracker>();
        }

        private void LateUpdate()
        {
            RefreshTargets();
        }

        private void RefreshTargets()
        {
            projectiles.Clear();
            destructibles.Clear();

            Vector2 center = transform.position;
            Vector2 forward = GetForwardDirection();

            int count = Physics2D.OverlapCircleNonAlloc(
                center,
                forwardRadius,
                results,
                detectionMask
            );

            for (int i = 0; i < count; i++)
            {
                Collider2D col = results[i];
                if (col == null) continue;

                // 자기 자신/자식 콜라이더 제외
                if (col.transform == transform || col.transform.IsChildOf(transform))
                    continue;

                Vector2 point = col.bounds.ClosestPoint(center);
                Vector2 toTarget = point - center;
                float dist = toTarget.magnitude;

                if (dist <= 0.0001f)
                    dist = Vector2.Distance((Vector2)col.transform.position, center);

                if (!IsInsideCustomZone(toTarget, dist, forward))
                    continue;

                var projectile = FindProjectileComponent(col);
                if (projectile != null && !projectiles.Contains(projectile))
                    projectiles.Add(projectile);

                var destructible = col.GetComponentInParent<Destructible>();
                if (destructible != null && !destructibles.Contains(destructible))
                    destructibles.Add(destructible);
            }

            projectiles.RemoveAll(p => p == null);
            destructibles.RemoveAll(d => d == null);
        }

        private Vector2 GetForwardDirection()
        {
            if (dirTracker != null)
            {
                Vector3 dir3 = dirTracker.LastMoveDir;
                Vector2 dir2 = new Vector2(dir3.x, dir3.y);

                if (dir2.sqrMagnitude > 0.0001f)
                    return dir2.normalized;
            }

            // 기본 전방 방향
            return Vector2.down;
        }

        private bool IsInsideCustomZone(Vector2 toTarget, float dist, Vector2 forward)
        {
            if (dist > forwardRadius) return false;

            if (toTarget.sqrMagnitude <= 0.0001f)
                return true;

            float angle = Vector2.Angle(forward, toTarget.normalized);

            float allowedRadius = (angle <= halfAngle) ? forwardRadius : sideBackRadius;
            return dist <= allowedRadius;
        }

        private MonoBehaviour FindProjectileComponent(Collider2D other)
        {
            var mbs = other.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < mbs.Length; i++)
            {
                if (mbs[i] is IPlayerDestroyableProjectile)
                    return mbs[i];
            }
            return null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector2 center = transform.position;
            Vector2 forward = Application.isPlaying ? GetForwardDirection() : Vector2.down;

            DrawZoneGizmo(center, forward);
        }

        private void DrawZoneGizmo(Vector2 center, Vector2 forward)
        {
            const int steps = 80;

            for (int i = 0; i < steps; i++)
            {
                float a0 = i * 360f / steps;
                float a1 = (i + 1) * 360f / steps;

                Vector2 d0 = DirFromAngle(a0);
                Vector2 d1 = DirFromAngle(a1);

                float r0 = Vector2.Angle(forward, d0) <= halfAngle ? forwardRadius : sideBackRadius;
                float r1 = Vector2.Angle(forward, d1) <= halfAngle ? forwardRadius : sideBackRadius;

                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(center + d0 * r0, center + d1 * r1);
            }
        }

        private Vector2 DirFromAngle(float deg)
        {
            float rad = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
#endif
    }
}