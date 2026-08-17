using System.Collections;
using UnityEngine;
using Dungeon.Player;

namespace Dungeon.Traps
{
    /// <summary>
    /// 3F 전용 레이저 함정.
    /// 벽/바닥/다른 함정 Collider는 전부 무시하고,
    /// 플레이어만 레이저 판정 대상으로 사용합니다.
    /// 플레이어가 발사 방향에 없으면 maxLaserDistance까지 그대로 표시됩니다.
    /// </summary>
    public class LaserCrystalTrap3F2D : MonoBehaviour
    {
        private enum TrapState
        {
            Idle,
            Charging,
            Firing,
            Cooldown
        }

        [Header("References")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private SpriteRenderer crystalMaskRenderer;
        [SerializeField] private SpriteRenderer laserBeamRenderer;

        [Header("Detection")]
        [SerializeField] private float detectRadius = 4f;
        [SerializeField] private LayerMask playerLayer;

        [Header("Laser")]
        [SerializeField] private float maxLaserDistance = 8f;
        [SerializeField] private int damage = 1;
        [SerializeField] private float laserDuration = 0.2f;

        [Header("Timing")]
        [SerializeField] private float chargeTime = 1.0f;
        [SerializeField] private float cooldown = 1.5f;

        [Header("Mask")]
        [Range(0f, 1f)]
        [SerializeField] private float idleMaskAlpha = 1f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        private TrapState state = TrapState.Idle;
        private Vector2 lockedDirection;

        private void Awake()
        {
            if (firePoint == null)
                firePoint = transform;

            if (laserBeamRenderer != null)
                laserBeamRenderer.enabled = false;

            SetMaskAlpha(idleMaskAlpha);
        }

        private void Update()
        {
            if (state != TrapState.Idle)
                return;

            Transform player = FindPlayerInRange();
            if (player == null)
                return;

            Vector2 origin = firePoint.position;
            Vector2 toPlayer = (Vector2)player.position - origin;

            if (toPlayer.sqrMagnitude <= 0.0001f)
                return;

            lockedDirection = toPlayer.normalized;
            StartCoroutine(AttackRoutine());
        }

        private Transform FindPlayerInRange()
        {
            Collider2D hit = Physics2D.OverlapCircle(
                transform.position,
                detectRadius,
                playerLayer
            );

            if (hit == null)
                return null;

            Health health = hit.GetComponentInParent<Health>();
            return health != null ? health.transform : null;
        }

        private IEnumerator AttackRoutine()
        {
            state = TrapState.Charging;

            yield return StartCoroutine(
                FadeMask(idleMaskAlpha, 0f, chargeTime)
            );

            state = TrapState.Firing;
            FireLaser(lockedDirection);

            yield return new WaitForSeconds(laserDuration);

            if (laserBeamRenderer != null)
                laserBeamRenderer.enabled = false;

            SetMaskAlpha(idleMaskAlpha);

            state = TrapState.Cooldown;
            yield return new WaitForSeconds(cooldown);

            state = TrapState.Idle;
        }

        private IEnumerator FadeMask(float fromAlpha, float toAlpha, float duration)
        {
            if (crystalMaskRenderer == null)
                yield break;

            if (duration <= 0f)
            {
                SetMaskAlpha(toAlpha);
                yield break;
            }

            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float ratio = Mathf.Clamp01(t / duration);
                SetMaskAlpha(Mathf.Lerp(fromAlpha, toAlpha, ratio));
                yield return null;
            }

            SetMaskAlpha(toAlpha);
        }

        private void SetMaskAlpha(float alpha)
        {
            if (crystalMaskRenderer == null)
                return;

            Color c = crystalMaskRenderer.color;
            c.a = alpha;
            crystalMaskRenderer.color = c;
        }

        private void FireLaser(Vector2 dir)
        {
            if (dir.sqrMagnitude <= 0.0001f)
                return;

            Vector2 origin = firePoint.position;

            // 핵심:
            // playerLayer만 Raycast 대상으로 사용하므로
            // Wall / Ground / Trap Collider는 레이저를 막지 못합니다.
            RaycastHit2D[] hits = Physics2D.RaycastAll(
                origin,
                dir,
                maxLaserDistance,
                playerLayer
            );

            float actualDistance = maxLaserDistance;

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null)
                    continue;

                Health health = hit.collider.GetComponentInParent<Health>();
                if (health == null)
                    continue;

                actualDistance = hit.distance;
                health.TakeDamage(damage);
                break;
            }

            ShowLaserVisual(origin, dir, actualDistance);
        }

        private void ShowLaserVisual(Vector2 origin, Vector2 dir, float distance)
        {
            if (laserBeamRenderer == null || laserBeamRenderer.sprite == null)
                return;

            laserBeamRenderer.enabled = true;
            laserBeamRenderer.transform.position = origin;
            laserBeamRenderer.transform.up = dir;

            float spriteHeight = laserBeamRenderer.sprite.bounds.size.y;
            if (spriteHeight <= 0f)
                spriteHeight = 1f;

            Vector3 scale = laserBeamRenderer.transform.localScale;
            scale.y = distance / spriteHeight;
            laserBeamRenderer.transform.localScale = scale;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
                return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectRadius);

            if (firePoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(firePoint.position, 0.05f);

                Gizmos.DrawLine(
                    firePoint.position,
                    firePoint.position + firePoint.up * maxLaserDistance
                );
            }
        }
    }
}
