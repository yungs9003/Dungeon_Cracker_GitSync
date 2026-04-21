using System.Collections.Generic;
using UnityEngine;
using Dungeon.Player;

namespace Dungeon.Traps
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class RollingBallTrap2D : MonoBehaviour
    {
        [Header("Lifetime (0이면 안 사라짐)")]
        [SerializeField] private float lifeTime = 4f;

        [Header("Damage")]
        [SerializeField] private int damage = 2;
        [SerializeField] private float hitCooldownPerTarget = 0.4f;

        [Header("Movement")]
        [SerializeField] private LayerMask playerMask = 0;
        [SerializeField] private LayerMask obstacleMask = 0;
        [SerializeField] private LayerMask pitMask = 0;

        [Header("Player collision ignore")]
        [SerializeField] private bool ignorePlayerPhysicsCollision = false;

        [Header("Knockback (Player)")]
        [SerializeField] private float knockbackDistance = 1.2f;
        [SerializeField] private float knockbackDuration = 0.12f;

        private Rigidbody2D rb;
        private Collider2D myCol;
        private Collider2D ownerCol;

        private Vector2 moveDir;
        private float moveSpeed;
        private bool launched;
        private bool stopped;

        private readonly Dictionary<Health, float> lastHitTime = new();

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            myCol = GetComponent<Collider2D>();

            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        public void Launch(Vector2 dir, float speed, int damage, Collider2D owner = null)
        {
            this.damage = damage;
            ownerCol = owner;

            if (ownerCol != null)
                Physics2D.IgnoreCollision(myCol, ownerCol, true);

            moveDir = dir.sqrMagnitude > 0f ? dir.normalized : Vector2.right;
            moveSpeed = Mathf.Max(0f, speed);
            launched = true;
            stopped = false;

            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = moveDir * moveSpeed;

            if (ignorePlayerPhysicsCollision)
                IgnorePlayerCollisions();

            if (lifeTime > 0f)
            {
                CancelInvoke(nameof(Despawn));
                Invoke(nameof(Despawn), lifeTime);
            }
        }

        private void FixedUpdate()
        {
            if (!launched || stopped) return;

            // 일정 속도로 직진
            rb.linearVelocity = moveDir * moveSpeed;
        }

        private void IgnorePlayerCollisions()
        {
            if (playerMask.value == 0) return;

            Collider2D[] myCols = GetComponentsInChildren<Collider2D>(true);
            Collider2D[] all = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None);

            for (int i = 0; i < all.Length; i++)
            {
                Collider2D c = all[i];
                if (c == null) continue;
                if ((playerMask.value & (1 << c.gameObject.layer)) == 0) continue;

                foreach (var mc in myCols)
                {
                    if (mc == null || mc.isTrigger) continue;
                    Physics2D.IgnoreCollision(mc, c, true);
                }
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!launched) return;

            Collider2D other = collision.collider;
            if (other == null) return;

            // 발사기 본체/부모/자식 콜라이더는 무시
            if (IsOwnerCollider(other))
                return;

            // 플레이어면 데미지 + 넉백
            if (IsPlayerLayer(other.gameObject.layer))
            {
                TryDamageAndKnockback(other);
                return;
            }

            // 장애물 레이어면 소멸
            if ((obstacleMask.value & (1 << other.gameObject.layer)) != 0)
            {
                Despawn();
                return;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!launched) return;
            if (other == null) return;

            // 발사기 본체/부모/자식 콜라이더는 무시
            if (IsOwnerCollider(other))
                return;

            // Pit 같은 Trigger 지형이면 소멸
            if ((pitMask.value & (1 << other.gameObject.layer)) != 0)
            {
                Despawn();
                return;
            }

            // 플레이어를 Trigger로 처리하고 싶을 때를 대비
            if (IsPlayerLayer(other.gameObject.layer))
            {
                TryDamageAndKnockback(other);
                return;
            }
        }

        private bool IsPlayerLayer(int layer)
        {
            if (playerMask.value == 0) return false;
            return (playerMask.value & (1 << layer)) != 0;
        }

        private bool IsOwnerCollider(Collider2D other)
        {
            if (ownerCol == null || other == null) return false;

            if (other == ownerCol)
                return true;

            Collider2D[] parentCols = ownerCol.GetComponentsInParent<Collider2D>(true);
            for (int i = 0; i < parentCols.Length; i++)
            {
                if (parentCols[i] == other)
                    return true;
            }

            Collider2D[] childCols = ownerCol.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < childCols.Length; i++)
            {
                if (childCols[i] == other)
                    return true;
            }

            return false;
        }

        private void TryDamageAndKnockback(Collider2D other)
        {
            if (other == null) return;
            if (other.isTrigger && !IsPlayerLayer(other.gameObject.layer)) return;

            Health health = other.GetComponentInParent<Health>();
            if (health == null) return;

            float now = Time.time;
            if (lastHitTime.TryGetValue(health, out float t) && now - t < hitCooldownPerTarget)
                return;

            lastHitTime[health] = now;
            health.TakeDamage(damage);

            var kb = other.GetComponentInParent<KnockbackReceiver>();
            if (kb != null)
            {
                Vector2 away = ((Vector2)other.transform.position - (Vector2)transform.position).normalized;
                kb.AddKnockback(new Vector3(away.x, away.y, 0f), knockbackDistance, knockbackDuration);
            }
        }

        private void Despawn()
        {
            Destroy(gameObject);
        }
    }
}