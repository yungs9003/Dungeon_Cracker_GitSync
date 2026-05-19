using System.Collections;
using UnityEngine;
using Dungeon.Player;

namespace Dungeon.Traps
{
    public class LaserCrystalTrap2D : MonoBehaviour
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
        [SerializeField] private LayerMask laserHitLayer;   // Player + Wall/Obstacle
        [SerializeField] private int damage = 1;
        [SerializeField] private float laserDuration = 0.2f;

        [Header("Timing")]
        [SerializeField] private float chargeTime = 1.0f;
        [SerializeField] private float cooldown = 1.5f;

        [Header("Mask")]
        [Range(0f, 1f)]
        [SerializeField] private float idleMaskAlpha = 1f;  // 평소엔 어둡게 꽉 덮임

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
            if (state != TrapState.Idle) return;

            Transform player = FindPlayerInRange();
            if (player == null) return;

            Vector2 origin = firePoint.position;
            lockedDirection = ((Vector2)player.position - origin).normalized;

            StartCoroutine(AttackRoutine());
        }

        private Transform FindPlayerInRange()
        {
            Collider2D hit = Physics2D.OverlapCircle(transform.position, detectRadius, playerLayer);
            if (hit == null) return null;

            return hit.transform;
        }

        private IEnumerator AttackRoutine()
        {
            state = TrapState.Charging;

            // 1) 마스크가 점점 투명해짐
            yield return StartCoroutine(FadeMask(idleMaskAlpha, 0f, chargeTime));

            // 2) 저장된 방향으로 레이저 발사
            state = TrapState.Firing;
            FireLaser(lockedDirection);

            yield return new WaitForSeconds(laserDuration);

            // 3) 레이저 끄기
            if (laserBeamRenderer != null)
                laserBeamRenderer.enabled = false;

            // 4) 다시 어두운 상태로 복귀
            SetMaskAlpha(idleMaskAlpha);

            // 5) 쿨타임
            state = TrapState.Cooldown;
            yield return new WaitForSeconds(cooldown);

            state = TrapState.Idle;
        }

        private IEnumerator FadeMask(float fromAlpha, float toAlpha, float duration)
        {
            if (crystalMaskRenderer == null)
                yield break;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float lerp = Mathf.Clamp01(t / duration);
                float alpha = Mathf.Lerp(fromAlpha, toAlpha, lerp);
                SetMaskAlpha(alpha);
                yield return null;
            }

            SetMaskAlpha(toAlpha);
        }

        private void SetMaskAlpha(float alpha)
        {
            if (crystalMaskRenderer == null) return;

            Color c = crystalMaskRenderer.color;
            c.a = alpha;
            crystalMaskRenderer.color = c;
        }

        private void FireLaser(Vector2 dir)
        {
            Vector2 origin = firePoint.position;

            RaycastHit2D hit = Physics2D.Raycast(origin, dir, maxLaserDistance, laserHitLayer);

            float actualDistance = maxLaserDistance;

            if (hit.collider != null)
            {
                actualDistance = hit.distance;

                Health health = hit.collider.GetComponentInParent<Health>();
                if (health != null)
                {
                    health.TakeDamage(damage);
                }
            }

            ShowLaserVisual(origin, dir, actualDistance);
        }

        private void ShowLaserVisual(Vector2 origin, Vector2 dir, float distance)
        {
            if (laserBeamRenderer == null || laserBeamRenderer.sprite == null)
                return;

            laserBeamRenderer.enabled = true;

            // 위치: 레이저 시작점
            laserBeamRenderer.transform.position = origin;

            // 방향: 레이저 스프라이트의 위쪽(up)이 dir을 향하도록
            laserBeamRenderer.transform.up = dir;

            // 길이 조절
            float spriteHeight = laserBeamRenderer.sprite.bounds.size.y;
            if (spriteHeight <= 0f) spriteHeight = 1f;

            Vector3 scale = laserBeamRenderer.transform.localScale;
            scale.y = distance / spriteHeight;
            laserBeamRenderer.transform.localScale = scale;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectRadius);

            if (firePoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(firePoint.position, 0.05f);
            }
        }
    }
}