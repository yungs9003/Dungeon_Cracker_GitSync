using UnityEngine;
using Dungeon.Core;
using Dungeon.Player;

namespace Dungeon.Traps
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class Arrow2D : MonoBehaviour, IPlayerDestroyableProjectile
    {
        [Header("Lifetime")]
        [SerializeField] private float lifeTime = 3f;

        [Header("Visual")]
        [SerializeField] private Transform visual;
        [SerializeField] private float visualAngleOffset = -90f; 
        // 화살 이미지가 위쪽(+Y)을 향할 때 권장값

        [Header("Hit timing")]
        [SerializeField] private float hitEnableDelay = 0.05f;

        private Rigidbody2D rb;
        private Collider2D myCol;
        private Collider2D ownerCol;
        private string sourceTrapCode;
        private bool hasHit;
        private float hitEnabledAt;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            myCol = GetComponent<Collider2D>();

            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            if (visual == null)
                visual = transform;
        }

        public void Launch(Vector2 dir, float speed, string trapCode, Collider2D owner = null)
        {
            sourceTrapCode = trapCode;
            ownerCol = owner;
            hasHit = false;
            hitEnabledAt = Time.time + hitEnableDelay;

            if (ownerCol != null)
            {
                foreach (var c in ownerCol.GetComponentsInParent<Collider2D>(true))
                    Physics2D.IgnoreCollision(myCol, c, true);

                foreach (var c in ownerCol.GetComponentsInChildren<Collider2D>(true))
                    Physics2D.IgnoreCollision(myCol, c, true);
            }

            dir = dir.sqrMagnitude > 0f ? dir.normalized : Vector2.right;
            rb.linearVelocity = dir * speed;
            AlignVisualToDirection(dir);

            CancelInvoke(nameof(Despawn));
            Invoke(nameof(Despawn), lifeTime);
        }

        private void AlignVisualToDirection(Vector2 dir)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            visual.rotation = Quaternion.Euler(0f, 0f, angle + visualAngleOffset);
        }

        private void OnTriggerEnter2D(Collider2D other) => TryHit(other);
        private void OnTriggerStay2D(Collider2D other) => TryHit(other);

        private void TryHit(Collider2D other)
        {
            if (hasHit) return;
            if (Time.time < hitEnabledAt) return;
            if (other.isTrigger) return;
            if (other.GetComponentInParent<Arrow2D>() != null) return;

            Health health = other.GetComponentInParent<Health>();
            if (health != null)
            {
                hasHit = true;
                TrapDamageEvent.Raise(sourceTrapCode, gameObject, health);
                Despawn();
                return;
            }

            hasHit = true;
            Despawn();
        }

        public void DestroyByPlayer()
        {
            Despawn();
        }

        private void Despawn()
        {
            Destroy(gameObject);
        }
    }
}
