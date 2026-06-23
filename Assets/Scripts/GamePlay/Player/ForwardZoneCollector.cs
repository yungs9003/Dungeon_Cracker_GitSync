using System.Collections.Generic;
using UnityEngine;
using Dungeon.Traps;
using Dungeon.Core;

namespace Dungeon.Player
{
    /// <summary>
    /// 플레이어 진행 방향 기준 시야/상호작용 범위를 계산합니다.
    ///
    /// 새 시야 형태:
    /// - 진행 반대방향 120도 영역은 작은 반지름(rearRadius = 0.8)
    /// - 나머지 전방 240도 영역은 큰 반지름(frontRadius = 2.0)
    /// - 두 원호는 방사형 직선이 아니라, 작은 원의 접선과 큰 원의 교점으로 연결합니다.
    ///
    /// 이 클래스의 IsWorldPointInsideZone / IsColliderInsideZone 결과를
    /// 삭제 범위, hidden tile reveal, vision mask가 공통으로 사용합니다.
    /// </summary>
    public class ForwardZoneCollector : MonoBehaviour
    {
        [Header("Direction")]
        [Tooltip("Player 오브젝트에 붙어 있는 PlayerMoveDirectionTracker2D를 넣으세요.")]
        [SerializeField] private PlayerMoveDirectionTracker2D dirTracker;

        [Tooltip("시야 중심으로 사용할 Transform입니다. 비워두면 dirTracker의 Transform을 사용하고, 그것도 없으면 이 오브젝트 위치를 사용합니다.")]
        [SerializeField] private Transform centerTransform;

        [Header("Zone Shape")]
        [Tooltip("전방 큰 시야 반지름입니다. 기존 forwardRadius와 같은 의미입니다.")]
        [SerializeField] private float forwardRadius = 2f;

        [Tooltip("후방 작은 시야 반지름입니다. 기존 sideBackRadius와 같은 의미입니다.")]
        [SerializeField] private float sideBackRadius = 0.8f;

        [Tooltip("진행 반대방향의 작은 원호 전체 각도입니다. 120이면 반대방향 ±60도입니다.")]
        [SerializeField, Range(10f, 180f)] private float rearArcAngle = 120f;

        [Header("Zone Outline Quality")]
        [Tooltip("후방 작은 원호 샘플 수입니다.")]
        [SerializeField, Range(4, 64)] private int rearArcSamples = 16;

        [Tooltip("전방 큰 원호 샘플 수입니다.")]
        [SerializeField, Range(8, 128)] private int frontArcSamples = 48;

        [Header("Detection")]
        [SerializeField] private LayerMask detectionMask = ~0;

        public readonly List<MonoBehaviour> projectiles = new();
        public readonly List<Destructible> destructibles = new();

        private readonly Collider2D[] results = new Collider2D[64];
        private readonly List<Vector2> cachedOutline = new(96);

        public Vector2 ZoneCenter => GetCenter();
        public float ForwardRadius => forwardRadius;
        public float SideBackRadius => sideBackRadius;

        // 기존 코드 호환용. 이제 halfAngle은 전방 부채꼴 각도가 아니라 후방 원호 절반 각도와 대응됩니다.
        public float HalfAngle => rearArcAngle * 0.5f;
        public float RearArcAngle => rearArcAngle;
        public Vector2 CurrentForwardDirection => GetForwardDirection();

        private void Awake()
        {
            AutoBindReferences();
        }

        private void OnEnable()
        {
            AutoBindReferences();
        }

        private void AutoBindReferences()
        {
            if (dirTracker == null)
                dirTracker = GetComponentInParent<PlayerMoveDirectionTracker2D>();

            if (dirTracker == null)
                dirTracker = FindFirstObjectByType<PlayerMoveDirectionTracker2D>();

            if (centerTransform == null && dirTracker != null)
                centerTransform = dirTracker.transform;
        }

        private void LateUpdate()
        {
            RefreshTargets();
        }

        private void RefreshTargets()
        {
            projectiles.Clear();
            destructibles.Clear();

            Vector2 center = GetCenter();
            float maxRadius = Mathf.Max(forwardRadius, sideBackRadius);

            int count = Physics2D.OverlapCircleNonAlloc(
                center,
                maxRadius,
                results,
                detectionMask
            );

            for (int i = 0; i < count; i++)
            {
                Collider2D col = results[i];
                if (col == null) continue;

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

        private Vector2 GetForwardDirection()
        {
            if (dirTracker != null)
            {
                Vector2 dir2 = dirTracker.LastMoveDir;

                if (dir2.sqrMagnitude > 0.0001f)
                    return dir2.normalized;
            }

            return Vector2.down;
        }

        public bool IsWorldPointInsideZone(Vector2 worldPoint)
        {
            Vector2 center = GetCenter();

            if ((worldPoint - center).sqrMagnitude <= 0.000001f)
                return true;

            List<Vector2> outline = BuildZoneOutlineWorld();
            return IsPointInPolygon(outline, worldPoint);
        }

        public bool IsColliderInsideZone(Collider2D col)
        {
            if (col == null) return false;

            Vector2 center = GetCenter();

            // 중심과 가장 가까운 점 하나만 보면 큰 콜라이더의 일부가 시야에 들어와도 놓칠 수 있어서
            // bounds 중심/모서리/ClosestPoint를 함께 검사합니다.
            Vector2 closest = col.bounds.ClosestPoint(center);
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
        /// 시야 외곽선을 월드 좌표 목록으로 반환합니다.
        /// 점 순서는 닫힌 폴리곤으로 사용할 수 있도록 정렬되어 있습니다.
        /// </summary>
        public List<Vector2> BuildZoneOutlineWorld()
        {
            cachedOutline.Clear();

            Vector2 center = GetCenter();
            Vector2 forward = GetForwardDirection();
            float forwardDeg = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;

            float rearRadius = Mathf.Max(0.001f, sideBackRadius);
            float frontRadius = Mathf.Max(rearRadius + 0.001f, forwardRadius);

            float halfRear = Mathf.Clamp(rearArcAngle, 10f, 180f) * 0.5f;
            float rearCenterDeg = forwardDeg + 180f;
            float rearStartDeg = rearCenterDeg - halfRear;
            float rearEndDeg = rearCenterDeg + halfRear;

            // 1) 뒤쪽 작은 원호
            AddArc(cachedOutline, center, rearRadius, rearStartDeg, rearEndDeg, rearArcSamples, skipFirstPoint: false);

            // 2) 작은 원호 끝점에서 큰 원까지 접선 연결
            Vector2 rearEndInner = center + DirFromAngle(rearEndDeg) * rearRadius;
            Vector2 frontStartOuter = GetFrontTangentPoint(center, rearEndDeg, rearRadius, frontRadius, forward);
            AddPointIfFar(cachedOutline, frontStartOuter);

            // 3) 전방 큰 원호
            Vector2 rearStartInner = center + DirFromAngle(rearStartDeg) * rearRadius;
            Vector2 frontEndOuter = GetFrontTangentPoint(center, rearStartDeg, rearRadius, frontRadius, forward);

            float frontStartDeg = AngleFromVector(frontStartOuter - center);
            float frontEndDeg = AngleFromVector(frontEndOuter - center);

            // frontStart -> frontEnd 방향은 전방을 지나가는 짧은 호가 아니라,
            // 오른쪽/전방 바깥쪽을 따라가는 래핑된 호여야 합니다.
            while (frontEndDeg <= frontStartDeg)
                frontEndDeg += 360f;

            AddArc(cachedOutline, center, frontRadius, frontStartDeg, frontEndDeg, frontArcSamples, skipFirstPoint: true);

            // 4) 큰 원호 끝점에서 작은 원호 시작점까지 접선 연결은 폴리곤 closing edge가 담당합니다.
            // 명시적으로 rearStartInner를 한 번 추가해 경계가 안정적으로 닫히도록 합니다.
            AddPointIfFar(cachedOutline, rearStartInner);

            return cachedOutline;
        }

        /// <summary>
        /// 특정 월드 방향으로 시야 외곽선까지의 거리입니다.
        /// VisionMask가 마스크 구멍의 반지름을 계산할 때 사용합니다.
        /// </summary>
        public float GetAllowedRadiusForWorldDirection(Vector2 worldDirection)
        {
            return GetZoneRadiusForWorldDirection(worldDirection);
        }

        public float GetAllowedRadiusAtWorldAngle(float worldAngleDegrees)
        {
            float rad = worldAngleDegrees * Mathf.Deg2Rad;
            return GetZoneRadiusForWorldDirection(new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)));
        }

        public float GetZoneRadiusForWorldDirection(Vector2 worldDirection)
        {
            Vector2 dir = worldDirection.sqrMagnitude > 0.000001f
                ? worldDirection.normalized
                : GetForwardDirection();

            Vector2 center = GetCenter();
            List<Vector2> outline = BuildZoneOutlineWorld();

            float best = -1f;

            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 a = outline[i];
                Vector2 b = outline[(i + 1) % outline.Count];

                if (TryRaySegmentIntersection(center, dir, a, b, out float t))
                {
                    if (t > best)
                        best = t;
                }
            }

            if (best > 0f)
                return best;

            // 예외 상황 fallback. 중심을 포함하는 폴리곤이라 정상적으로는 거의 도달하지 않습니다.
            return Mathf.Max(forwardRadius, sideBackRadius);
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

        private static void AddArc(
            List<Vector2> points,
            Vector2 center,
            float radius,
            float startDeg,
            float endDeg,
            int samples,
            bool skipFirstPoint)
        {
            samples = Mathf.Max(2, samples);

            for (int i = 0; i <= samples; i++)
            {
                if (skipFirstPoint && i == 0)
                    continue;

                float t = i / (float)samples;
                float deg = Mathf.Lerp(startDeg, endDeg, t);
                AddPointIfFar(points, center + DirFromAngle(deg) * radius);
            }
        }

        private static Vector2 GetFrontTangentPoint(
            Vector2 center,
            float innerAngleDeg,
            float innerRadius,
            float outerRadius,
            Vector2 forward)
        {
            Vector2 e = DirFromAngle(innerAngleDeg);
            Vector2 p = center + e * innerRadius;

            float h = Mathf.Sqrt(Mathf.Max(0f, outerRadius * outerRadius - innerRadius * innerRadius));
            Vector2 n = new Vector2(-e.y, e.x);

            Vector2 candidateA = p + n * h;
            Vector2 candidateB = p - n * h;

            // 두 교점 중 진행방향 쪽에 더 가까운 교점을 선택합니다.
            float dotA = Vector2.Dot(candidateA - center, forward);
            float dotB = Vector2.Dot(candidateB - center, forward);

            return dotA >= dotB ? candidateA : candidateB;
        }

        private static void AddPointIfFar(List<Vector2> points, Vector2 point)
        {
            if (points.Count > 0 && Vector2.Distance(points[points.Count - 1], point) < 0.0001f)
                return;

            points.Add(point);
        }

        private static Vector2 DirFromAngle(float deg)
        {
            float rad = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        private static float AngleFromVector(Vector2 v)
        {
            float deg = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            if (deg < 0f) deg += 360f;
            return deg;
        }

        private static bool IsPointInPolygon(List<Vector2> poly, Vector2 p)
        {
            if (poly == null || poly.Count < 3)
                return false;

            bool inside = false;

            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                Vector2 pi = poly[i];
                Vector2 pj = poly[j];

                bool intersect =
                    ((pi.y > p.y) != (pj.y > p.y)) &&
                    (p.x < (pj.x - pi.x) * (p.y - pi.y) / ((pj.y - pi.y) + 0.000001f) + pi.x);

                if (intersect)
                    inside = !inside;
            }

            return inside;
        }

        private static bool TryRaySegmentIntersection(
            Vector2 rayOrigin,
            Vector2 rayDir,
            Vector2 segA,
            Vector2 segB,
            out float rayT)
        {
            rayT = 0f;

            Vector2 v1 = rayOrigin - segA;
            Vector2 v2 = segB - segA;
            Vector2 v3 = new Vector2(-rayDir.y, rayDir.x);

            float dot = Vector2.Dot(v2, v3);
            if (Mathf.Abs(dot) < 0.000001f)
                return false;

            float t1 = Cross(v2, v1) / dot;
            float t2 = Vector2.Dot(v1, v3) / dot;

            if (t1 >= 0f && t2 >= 0f && t2 <= 1f)
            {
                rayT = t1;
                return true;
            }

            return false;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector2 center = Application.isPlaying
                ? GetCenter()
                : (centerTransform != null ? (Vector2)centerTransform.position : (Vector2)transform.position);

            Vector2 forward = Application.isPlaying ? GetForwardDirection() : Vector2.down;
            DrawZoneGizmo(center, forward);
        }

        private void DrawZoneGizmo(Vector2 center, Vector2 forward)
        {
            List<Vector2> outline = BuildZoneOutlinePreview(center, forward);
            if (outline == null || outline.Count < 2)
                return;

            Gizmos.color = Color.cyan;
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 a = outline[i];
                Vector2 b = outline[(i + 1) % outline.Count];
                Gizmos.DrawLine(a, b);
            }
        }

        private List<Vector2> BuildZoneOutlinePreview(Vector2 center, Vector2 forward)
        {
            List<Vector2> points = new List<Vector2>(96);

            float forwardDeg = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
            float rearRadius = Mathf.Max(0.001f, sideBackRadius);
            float frontRadius = Mathf.Max(rearRadius + 0.001f, forwardRadius);
            float halfRear = Mathf.Clamp(rearArcAngle, 10f, 180f) * 0.5f;
            float rearCenterDeg = forwardDeg + 180f;
            float rearStartDeg = rearCenterDeg - halfRear;
            float rearEndDeg = rearCenterDeg + halfRear;

            AddArc(points, center, rearRadius, rearStartDeg, rearEndDeg, rearArcSamples, false);

            Vector2 frontStartOuter = GetFrontTangentPoint(center, rearEndDeg, rearRadius, frontRadius, forward);
            AddPointIfFar(points, frontStartOuter);

            Vector2 frontEndOuter = GetFrontTangentPoint(center, rearStartDeg, rearRadius, frontRadius, forward);
            float frontStartDeg = AngleFromVector(frontStartOuter - center);
            float frontEndDeg = AngleFromVector(frontEndOuter - center);
            while (frontEndDeg <= frontStartDeg)
                frontEndDeg += 360f;

            AddArc(points, center, frontRadius, frontStartDeg, frontEndDeg, frontArcSamples, true);
            AddPointIfFar(points, center + DirFromAngle(rearStartDeg) * rearRadius);

            return points;
        }
#endif
    }
}
