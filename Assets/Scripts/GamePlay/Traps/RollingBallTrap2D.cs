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

        [Header("Hit")]
        [SerializeField] private float hitCooldownPerTarget = 0.4f;

        [Header("Movement")]
        [SerializeField] private LayerMask playerMask = 0;
        [SerializeField] private LayerMask obstacleMask = 0;
        [SerializeField] private LayerMask pitMask = 0;

        [Header("Player collision ignore")]
        [SerializeField] private bool ignorePlayerPhysicsCollision = false;

        private Rigidbody2D rb;
        private Collider2D myCol;
        private Collider2D ownerCol;

        private Vector2 moveDir;
        private float moveSpeed;
        private bool launched;
        private bool stopped;
        private string sourceTrapCode;

        private readonly Dictionary<Health, float> lastHitTime = new();

        // IgnorePlayerPhysicsCollision을 켜면 OnCollisionEnter2D가 호출되지 않을 수 있으므로,
        // FixedUpdate에서 직접 플레이어와의 겹침을 검사하기 위한 콜라이더 목록/버퍼.
        private Collider2D[] myDamageCols;
        private readonly Collider2D[] overlapResults = new Collider2D[16];

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            myCol = GetComponent<Collider2D>();

            // 본체 Collider와 자식 DamageTrigger Collider를 모두 포함한다.
            myDamageCols = GetComponentsInChildren<Collider2D>(true);

            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        public void Launch(Vector2 dir, float speed, string trapCode, Collider2D owner = null)
        {
            sourceTrapCode = trapCode;
            ownerCol = owner;

            if (ownerCol != null)
                Physics2D.IgnoreCollision(myCol, ownerCol, true);

            moveDir = dir.sqrMagnitude > 0f ? dir.normalized : Vector2.right;
            moveSpeed = Mathf.Max(0f, speed);
            launched = true;
            stopped = false;

            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = moveDir * moveSpeed;

            // 발사 이후 자식 DamageTrigger가 생성/활성화된 경우까지 대비해서 한 번 더 갱신.
            myDamageCols = GetComponentsInChildren<Collider2D>(true);

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

            // IgnorePlayerPhysicsCollision이 켜져도 데미지는 들어가도록 직접 겹침 검사.
            CheckPlayerOverlapDamage();
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

        private void CheckPlayerOverlapDamage()
        {
            if (playerMask.value == 0) return;

            if (myDamageCols == null || myDamageCols.Length == 0)
                myDamageCols = GetComponentsInChildren<Collider2D>(true);

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(playerMask);
            filter.useLayerMask = true;
            filter.useTriggers = true;

            for (int i = 0; i < myDamageCols.Length; i++)
            {
                Collider2D col = myDamageCols[i];
                if (col == null) continue;
                if (!col.enabled) continue;

                int count = col.Overlap(filter, overlapResults);

                for (int j = 0; j < count; j++)
                {
                    Collider2D other = overlapResults[j];
                    if (other == null) continue;

                    if (IsOwnerCollider(other))
                        continue;

                    if (IsPlayerLayer(other.gameObject.layer))
                    {
                        TryDamageAndKnockback(other);
                    }

                    // 다음 검사에 이전 결과가 남지 않도록 비워둔다.
                    overlapResults[j] = null;
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

            // 플레이어를 Trigger로 처리하고 싶을 때를 대비
            if (IsPlayerLayer(other.gameObject.layer))
            {
                TryDamageAndKnockback(other);
                return;
            }
            // Pit 같은 Trigger 지형이면 소멸
            if ((pitMask.value & (1 << other.gameObject.layer)) != 0)
            {
                Despawn();
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
            TrapDamageEvent.Raise(sourceTrapCode, gameObject, health);
        }

        private void Despawn()
        {
            Destroy(gameObject);
        }
    }
}
