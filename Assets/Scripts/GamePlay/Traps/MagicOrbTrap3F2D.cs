using System.Collections;
using UnityEngine;
using Dungeon.Player;

namespace Dungeon.Traps
{
    /// <summary>
    /// 3F 전용 마법구 함정.
    /// 플레이어를 벽 너머에서도 감지하고,
    /// MagicOrbProjectile3F2D를 플레이어 방향으로 발사합니다.
    /// </summary>
    public class MagicOrbTrap3F2D : MonoBehaviour
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
        [SerializeField] private SpriteRenderer orbMaskRenderer;
        [SerializeField] private MagicOrbProjectile3F2D projectilePrefab;

        [Header("Detection")]
        [SerializeField] private float detectRadius = 4f;
        [SerializeField] private LayerMask playerLayer;

        [Header("Projectile")]
        [SerializeField] private float projectileSpeed = 2.5f;
        [SerializeField] private int damage = 1;

        [Tooltip("투사체가 이 거리만큼 이동하면 자동으로 삭제됩니다.")]
        [SerializeField] private float maxProjectileDistance = 8f;

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
            FireProjectile(lockedDirection);

            SetMaskAlpha(idleMaskAlpha);

            state = TrapState.Cooldown;
            yield return new WaitForSeconds(cooldown);

            state = TrapState.Idle;
        }

        private IEnumerator FadeMask(float fromAlpha, float toAlpha, float duration)
        {
            if (orbMaskRenderer == null)
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
                float alpha = Mathf.Lerp(fromAlpha, toAlpha, ratio);

                SetMaskAlpha(alpha);
                yield return null;
            }

            SetMaskAlpha(toAlpha);
        }

        private void SetMaskAlpha(float alpha)
        {
            if (orbMaskRenderer == null)
                return;

            Color c = orbMaskRenderer.color;
            c.a = alpha;
            orbMaskRenderer.color = c;
        }

        private void FireProjectile(Vector2 dir)
        {
            if (projectilePrefab == null)
            {
                Debug.LogWarning(
                    "[MagicOrbTrap3F2D] projectilePrefab이 연결되지 않았습니다."
                );
                return;
            }

            if (dir.sqrMagnitude <= 0.0001f)
                return;

            Vector3 spawnPos = firePoint != null
                ? firePoint.position
                : transform.position;

            MagicOrbProjectile3F2D projectile = Instantiate(
                projectilePrefab,
                spawnPos,
                Quaternion.identity
            );

            Collider2D[] ownerColliders =
                GetComponentsInChildren<Collider2D>(true);

            projectile.IgnoreOwnerColliders(ownerColliders);

            projectile.Launch(
                dir,
                projectileSpeed,
                damage,
                maxProjectileDistance
            );
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
                return;

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, detectRadius);

            if (firePoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(firePoint.position, 0.05f);
            }
        }
    }
}
