using System.Collections.Generic;
using UnityEngine;
using Dungeon.Traps;
using Dungeon.Core;

namespace Dungeon.Player
{
    /// <summary>
    /// 플레이어 중심 기준의 원형 상호작용/발견 범위를 계산합니다.
    ///
    /// 현재 버전에서는 진행 방향을 보지 않습니다.
    /// - clearRadius: 플레이어 중심에서 정상적으로 보이고 상호작용/발견되는 반지름 r
    /// - fadeBandRadius: 조명 마스크에서 r 바깥이 어두워지는 거리 r'
    ///
    /// ForwardZoneVisionMask2D는 clearRadius~clearRadius+fadeBandRadius 구간을
    /// 원형 그라데이션으로 어둡게 만들고, 그 바깥은 검은색으로 덮습니다.
    /// </summary>
    public class ForwardZoneCollector : MonoBehaviour
    {
        [Header("Center")]
        [Tooltip("시야 중심으로 사용할 Transform입니다. 보통 Player Transform을 넣습니다. 비워두면 자동으로 찾습니다.")]
        [SerializeField] private Transform centerTransform;

        [Tooltip("호환용 필드입니다. 현재 원형 조명에서는 방향을 사용하지 않지만, 기존 연결이 깨지지 않도록 유지합니다.")]
        [SerializeField] private PlayerMoveDirectionTracker2D dirTracker;

        [Header("Circular Zone")]
        [Tooltip("정상적으로 보이고, hidden trap reveal/삭제 판정에 사용하는 반지름 r입니다.")]
        [SerializeField] private float forwardRadius = 2f;

        [Tooltip("r 바깥에서 완전 암전까지 이어지는 조명 감쇠 거리 r'입니다. 1이면 중심 기준 2~3 사이가 그라데이션입니다.")]
        [SerializeField] private float sideBackRadius = 1f;

        [Tooltip("Scene View에서 원형 범위를 그릴 때 사용할 샘플 수입니다.")]
        [SerializeField, Range(16, 192)] private int circleOutlineSegments = 96;

        [Header("Detection")]
        [Tooltip("ForwardZone 안에서 수집할 투사체/파괴 가능 오브젝트 레이어입니다.")]
        [SerializeField] private LayerMask detectionMask = ~0;

        public readonly List<MonoBehaviour> projectiles = new();
        public readonly List<Destructible> destructibles = new();

        private readonly Collider2D[] results = new Collider2D[64];
        private readonly List<Vector2> cachedOutline = new(128);

        public Vector2 ZoneCenter => GetCenter();

        // 기존 코드와의 호환을 위해 이름은 유지합니다.
        // 의미는 이제 "전방 반지름"이 아니라 "정상 표시/판정 반지름"입니다.
        public float ForwardRadius => ClearRadius;
        public float SideBackRadius => FadeBandRadius;

        public float ClearRadius => Mathf.Max(0.01f, forwardRadius);
        public float FadeBandRadius => Mathf.Max(0f, sideBackRadius);
        public float OuterLightRadius => ClearRadius + FadeBandRadius;

        // 방향 기반 시야에서 쓰던 값입니다. 현재 원형 조명에서는 실제 판정에 사용하지 않습니다.
        public float HalfAngle => 180f;
        public float RearArcAngle => 360f;
        public Vector2 CurrentForwardDirection => GetForwardDirection();

        private void Awake()
        {
            AutoBindReferences();
        }

        private void OnEnable()
        {
            AutoBindReferences();
        }

        private void LateUpdate()
        {
            RefreshTargets();
        }

        private void AutoBindReferences()
        {
            if (centerTransform == null)
            {
                if (dirTracker == null)
                    dirTracker = GetComponentInParent<PlayerMoveDirectionTracker2D>();

                if (dirTracker == null)
                    dirTracker = FindFirstObjectByType<PlayerMoveDirectionTracker2D>();

                if (dirTracker != null)
                    centerTransform = dirTracker.transform;
            }
        }

        /// <summary>
        /// 현재 원형 판정 범위 안에 들어온 투사체와 파괴 가능 오브젝트를 갱신합니다.
        /// PlayerClickActionDestroyOne 같은 스크립트가 이 목록을 사용합니다.
        /// </summary>
        private void RefreshTargets()
        {
            projectiles.Clear();
            destructibles.Clear();

            Vector2 center = GetCenter();
            float radius = ClearRadius;

            int count = Physics2D.OverlapCircleNonAlloc(
                center,
                radius,
                results,
                detectionMask
            );

            for (int i = 0; i < count; i++)
            {
                Collider2D col = results[i];
                if (col == null) continue;

                // ForwardZone 자신 또는 플레이어 본체 콜라이더는 수집하지 않습니다.
                if (col.transform == transform || col.transform.IsChildOf(transform))
                    continue;

                if (centerTransform != null && (col.transform == centerTransform || col.transform.IsChildOf(centerTransform)))
                    continue;

                if (!IsColliderInsideZone(col))
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

        private Vector2 GetCenter()
        {
            if (centerTransform != null)
                return centerTransform.position;

            return transform.position;
        }

        /// <summary>
        /// 현재 원형 판정에서는 방향을 쓰지 않습니다.
        /// 다만 기존 스크립트/인스펙터 호환을 위해 값을 제공합니다.
        /// </summary>
        private Vector2 GetForwardDirection()
        {
            if (dirTracker != null)
            {
                Vector2 dir = dirTracker.LastMoveDir;
                if (dir.sqrMagnitude > 0.0001f)
                    return dir.normalized;
            }

            return Vector2.down;
        }

        /// <summary>
        /// 월드 좌표가 정상 표시/상호작용 반지름 r 안에 있는지 검사합니다.
        /// hidden trap reveal도 기본적으로 이 범위에 들어왔을 때 발생합니다.
        /// </summary>
        public bool IsWorldPointInsideZone(Vector2 worldPoint)
        {
            Vector2 center = GetCenter();
            float radius = ClearRadius;
            return (worldPoint - center).sqrMagnitude <= radius * radius;
        }

        /// <summary>
        /// 콜라이더의 일부가 원형 판정 범위 안에 들어왔는지 검사합니다.
        /// 큰 콜라이더가 경계에 걸치는 경우를 위해 ClosestPoint와 bounds 샘플을 같이 확인합니다.
        /// </summary>
        public bool IsColliderInsideZone(Collider2D col)
        {
            if (col == null) return false;

            Vector2 center = GetCenter();

            if (col.OverlapPoint(center))
                return true;

            Vector2 closest = col.ClosestPoint(center);
            if (IsWorldPointInsideZone(closest))
                return true;

            Bounds b = col.bounds;
            if (IsWorldPointInsideZone(b.center)) return true;
            if (IsWorldPointInsideZone(new Vector2(b.min.x, b.min.y))) return true;
            if (IsWorldPointInsideZone(new Vector2(b.min.x, b.max.y))) return true;
            if (IsWorldPointInsideZone(new Vector2(b.max.x, b.min.y))) return true;
            if (IsWorldPointInsideZone(new Vector2(b.max.x, b.max.y))) return true;

            return false;
        }

        /// <summary>
        /// 원형 시야의 외곽선을 월드 좌표 목록으로 반환합니다.
        /// 마스크나 Gizmo가 필요할 때 사용할 수 있습니다.
        /// </summary>
        public List<Vector2> BuildZoneOutlineWorld()
        {
            cachedOutline.Clear();

            Vector2 center = GetCenter();
            int segments = Mathf.Max(16, circleOutlineSegments);
            float radius = ClearRadius;

            for (int i = 0; i < segments; i++)
            {
                float deg = i * 360f / segments;
                cachedOutline.Add(center + DirectionFromAngle(deg) * radius);
            }

            return cachedOutline;
        }

        /// <summary>
        /// 특정 방향으로 정상 표시되는 거리입니다.
        /// 원형 조명이므로 모든 방향에서 clearRadius를 반환합니다.
        /// </summary>
        public float GetAllowedRadiusForWorldDirection(Vector2 worldDirection)
        {
            return ClearRadius;
        }

        public float GetAllowedRadiusAtWorldAngle(float worldAngleDegrees)
        {
            return ClearRadius;
        }

        public float GetZoneRadiusForWorldDirection(Vector2 worldDirection)
        {
            return ClearRadius;
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

        private static Vector2 DirectionFromAngle(float deg)
        {
            float rad = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector2 center = Application.isPlaying
                ? GetCenter()
                : (centerTransform != null ? (Vector2)centerTransform.position : (Vector2)transform.position);

            DrawCircle(center, ClearRadius, Color.cyan);

            if (FadeBandRadius > 0.001f)
                DrawCircle(center, OuterLightRadius, new Color(0f, 0.5f, 1f, 0.65f));
        }

        private static void DrawCircle(Vector2 center, float radius, Color color)
        {
            const int segments = 96;
            Gizmos.color = color;

            Vector2 prev = center + DirectionFromAngle(0f) * radius;
            for (int i = 1; i <= segments; i++)
            {
                float deg = i * 360f / segments;
                Vector2 next = center + DirectionFromAngle(deg) * radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
#endif
    }
}
