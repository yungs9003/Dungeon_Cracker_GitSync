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

        public void RespawnToStart()
        {
            TeleportToRaw(startRespawnPoint);
        }

        public void RespawnToLastSafePoint()
        {
            TeleportToRaw(lastSafePoint);
        }

        public bool ReturnToLastSafePointFromTrap(int damage = 0)
        {
            if (Time.time < nextTrapAllowedTime)
                return false;

            nextTrapAllowedTime = Time.time + trapCooldown;

            if (damage > 0 && health != null)
                health.TakeDamage(damage);

            // 데미지로 죽었으면 onDied -> 시작점 복귀를 우선시
            if (health != null && health.currentHP <= 0)
                return false;

            TeleportToRaw(lastSafePoint);
            return true;
        }

        public void TeleportToRaw(Vector2 worldPos)
        {
            if (respawnCo != null)
                StopCoroutine(respawnCo);

            respawnCo = StartCoroutine(CoTeleport(worldPos));
            health.ReviveFull();
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
    }
}