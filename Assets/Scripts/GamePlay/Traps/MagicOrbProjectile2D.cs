using UnityEngine;
using Dungeon.Player;

namespace Dungeon.Traps
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class MagicOrbProjectile2D : MonoBehaviour
    {
        [Header("Hit")]
        [SerializeField] private LayerMask destroyOnHitLayer;

        private Rigidbody2D rb;
        private Collider2D projectileCollider;

        private string sourceTrapCode;
        private bool hasHit;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            projectileCollider = GetComponent<Collider2D>();

            projectileCollider.isTrigger = true;
        }

        public void IgnoreOwnerColliders(Collider2D[] ownerColliders)
        {
            if (projectileCollider == null)
                projectileCollider = GetComponent<Collider2D>();

            if (ownerColliders == null) return;

            foreach (Collider2D ownerCol in ownerColliders)
            {
                if (ownerCol == null) continue;
                Physics2D.IgnoreCollision(projectileCollider, ownerCol, true);
            }
        }

        public void Launch(Vector2 dir, float speed, string trapCode, float lifeTime)
        {
            sourceTrapCode = trapCode;

            dir.Normalize();

            rb.gravityScale = 0f;
            rb.linearVelocity = dir * speed;

            RotateToDirection(dir);

            if (lifeTime > 0f)
                Destroy(gameObject, lifeTime);
        }

        private void RotateToDirection(Vector2 dir)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (hasHit) return;

            Health health = other.GetComponentInParent<Health>();

            if (health != null)
            {
                hasHit = true;
                TrapDamageEvent.Raise(sourceTrapCode, gameObject, health);
                Destroy(gameObject);
                return;
            }

            bool shouldDestroy =
                (destroyOnHitLayer.value & (1 << other.gameObject.layer)) != 0;

            if (shouldDestroy)
            {
                hasHit = true;
                Destroy(gameObject);
            }
        }
    }
}
