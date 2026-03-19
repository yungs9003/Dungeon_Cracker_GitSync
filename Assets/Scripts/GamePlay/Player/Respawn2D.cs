using System.Collections;
using UnityEngine;

namespace Dungeon.Player
{
    [RequireComponent(typeof(Health), typeof(Rigidbody2D))]
    public class Respawn2D : MonoBehaviour
    {
        [Header("Start Respawn Point (죽었을 때)")]
        [SerializeField] private Vector2 startRespawnPoint;

        [Header("Last Safe Point (낭떠러지/트랩 복귀용)")]
        [SerializeField] private Vector2 lastSafePoint;

        [Header("Option")]
        [SerializeField] private bool respawnOnDie = true;
        [SerializeField] private bool resetVelocity = true;

        [Header("Pit/Trap Anti-spam")]
        [SerializeField] private float trapCooldown = 0.2f;

        [Header("Teleport collision check (벽 안 방지)")]
        [SerializeField] private float playerCheckRadius = 0.35f;
        [SerializeField] private LayerMask solidMask = ~0;

        private float nextTrapAllowedTime;

        private Health health;
        private Rigidbody2D rb;
        private Coroutine respawnCo;

        private void Awake()
        {
            health = GetComponent<Health>();
            rb = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            if (health != null && respawnOnDie)
                health.onDied.AddListener(HandleDied);
        }

        private void OnDisable()
        {
            if (health != null && respawnOnDie)
                health.onDied.RemoveListener(HandleDied);
        }

        public void SetRespawnPoint(Vector2 p)
        {
            startRespawnPoint = p;

            if (lastSafePoint == Vector2.zero)
                lastSafePoint = p;
        }

        public void SetLastSafePoint(Vector2 p)
        {
            lastSafePoint = p;
        }

        /// <summary>
        /// 마지막 이동 방향의 반대로 backDistance만큼 되돌린 뒤,
        /// 벽과 겹치면 안전한 위치를 찾아 보정한다.
        /// </summary>
        public void TrapReturnByDirection(int damage, Vector2 lastMoveDir, float backDistance)
        {
            if (Time.time < nextTrapAllowedTime) return;
            nextTrapAllowedTime = Time.time + trapCooldown;

            if (damage > 0 && health != null)
                health.TakeDamage(damage);

            if (lastMoveDir.sqrMagnitude < 1e-6f)
            {
                var tracker = GetComponent<PlayerMoveDirectionTracker2D>();
                if (tracker != null)
                    lastMoveDir = tracker.LastMoveDir;
                else
                    lastMoveDir = Vector2.down;
            }

            lastMoveDir.Normalize();

            Vector2 basePos = rb.position;
            Vector2 candidate = basePos - lastMoveDir * backDistance;

            Vector2 safe = FindNonOverlappingPosition(basePos, candidate, 6);
            TeleportToRaw(safe);
        }

        public void RespawnToStart()
        {
            TeleportToRaw(startRespawnPoint);
        }

        public void RespawnToLastSafePoint()
        {
            TeleportToRaw(lastSafePoint);
        }

        public void TeleportToRaw(Vector2 worldPos)
        {
            if (respawnCo != null)
                StopCoroutine(respawnCo);

            respawnCo = StartCoroutine(CoTeleport(worldPos));
        }

        private IEnumerator CoTeleport(Vector2 worldPos)
        {
            if (resetVelocity && rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            var kb = GetComponent<KnockbackReceiver2D>();
            if (kb != null)
                kb.CancelKnockback();

            rb.position = worldPos;
            transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z);

            yield return null;

            if (resetVelocity && rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            respawnCo = null;
        }

        private void HandleDied()
        {
            RespawnToStart();
        }

        private Vector2 FindNonOverlappingPosition(Vector2 basePos, Vector2 preferred, int steps)
        {
            if (!IsOverlapping(preferred))
                return preferred;

            if (!IsOverlapping(lastSafePoint))
                return lastSafePoint;

            if (!IsOverlapping(startRespawnPoint))
                return startRespawnPoint;

            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 p = Vector2.Lerp(basePos, preferred, t);
                if (!IsOverlapping(p))
                    return p;
            }

            return startRespawnPoint;
        }

        private bool IsOverlapping(Vector2 pos)
        {
            return Physics2D.OverlapCircle(pos, playerCheckRadius, solidMask) != null;
        }
    }
}