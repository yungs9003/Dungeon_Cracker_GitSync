using UnityEngine;
using Dungeon.Player;

namespace Dungeon.Traps
{
    /// <summary>
    /// 3F 전용 마법구 투사체.
    /// 다른 타일/벽/함정과 겹쳐도 아무 일도 일어나지 않으며,
    /// 플레이어와 충돌하거나 maxTravelDistance를 이동했을 때만 삭제됩니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class MagicOrbProjectile3F2D : MonoBehaviour
    {
        [Header("Hit")]
        [Tooltip("플레이어가 사용하는 Layer만 지정합니다.")]
        [SerializeField] private LayerMask playerLayer;

        private Rigidbody2D rb;
        private Collider2D projectileCollider;

        private string sourceTrapCode;
        private float maxTravelDistance;
        private Vector2 launchPosition;
        private bool launched;
        private bool hasHit;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            projectileCollider = GetComponent<Collider2D>();

            projectileCollider.isTrigger = true;

            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void Update()
        {
            if (!launched || hasHit)
                return;

            float travelled = Vector2.Distance(
                launchPosition,
                transform.position
            );

            if (travelled >= maxTravelDistance)
            {
                hasHit = true;
                Destroy(gameObject);
            }
        }

        public void IgnoreOwnerColliders(Collider2D[] ownerColliders)
        {
            if (projectileCollider == null)
                projectileCollider = GetComponent<Collider2D>();

            if (ownerColliders == null)
                return;

            foreach (Collider2D ownerCol in ownerColliders)
            {
                if (ownerCol == null)
                    continue;

                Physics2D.IgnoreCollision(
                    projectileCollider,
                    ownerCol,
                    true
                );
            }
        }

        public void Launch(
            Vector2 dir,
            float speed,
            string trapCode,
            float maxDistance)
        {
            if (dir.sqrMagnitude <= 0.0001f || speed <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            sourceTrapCode = trapCode;
            maxTravelDistance = Mathf.Max(0.01f, maxDistance);

            dir.Normalize();

            launchPosition = transform.position;
            launched = true;

            rb.gravityScale = 0f;
            rb.linearVelocity = dir * speed;

            RotateToDirection(dir);
        }

        private void RotateToDirection(Vector2 dir)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (hasHit || other == null)
                return;

            // Player Layer가 아니면 벽/바닥/다른 함정 포함 전부 무시합니다.
            bool isPlayerLayer =
                (playerLayer.value & (1 << other.gameObject.layer)) != 0;

            if (!isPlayerLayer)
                return;

            Health health = other.GetComponentInParent<Health>();
            if (health == null)
                return;

            hasHit = true;
            rb.linearVelocity = Vector2.zero;

            TrapDamageEvent.Raise(sourceTrapCode, gameObject, health);
            Destroy(gameObject);
        }
    }
}
