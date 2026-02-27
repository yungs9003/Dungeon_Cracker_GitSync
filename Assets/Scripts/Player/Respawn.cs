using System.Collections;
using UnityEngine;

namespace Dungeon.Player;

[RequireComponent(typeof(Health), typeof(Rigidbody))]
public class Respawn : MonoBehaviour
{
    [Header("Start Respawn Point (죽었을 때)")]
    [SerializeField] private Vector3 startRespawnPoint;

    [Header("Last Safe Point (낭떠러지/트랩 복귀용)")]
    [SerializeField] private Vector3 lastSafePoint;

    [Header("Option")]
    [SerializeField] private bool respawnOnDie = true;
    [SerializeField] private bool resetVelocity = true;

    [Header("Pit/Trap Anti-spam")]
    [SerializeField] private float trapCooldown = 0.2f;
    [SerializeField] private float teleportUpOffset = 0.2f;

    [Header("Teleport collision check (벽 안 방지)")]
    [SerializeField] private float playerCheckRadius = 0.35f;
    [SerializeField] private float playerCheckHeight = 1.6f;
    [SerializeField] private LayerMask solidMask = ~0; // ✅ 벽/맵 레이어만 넣는 걸 추천

    private float nextTrapAllowedTime;

    private Health health;
    private Rigidbody rb;
    private Coroutine respawnCo;

    private void Awake()
    {
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
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

    // JsonBlockBuilder3D에서 start로 호출
    public void SetRespawnPoint(Vector3 p)
    {
        startRespawnPoint = p;
        if (lastSafePoint == Vector3.zero)
            lastSafePoint = p;
    }

    public void SetLastSafePoint(Vector3 p)
    {
        lastSafePoint = p;
    }

    // ✅ 방향 기반 트랩 복귀 + 벽 안 방지 보정
    public void TrapReturnByDirection(int damage, Vector3 lastMoveDir, float backDistance, float upOffset)
    {
        if (Time.time < nextTrapAllowedTime) return;
        nextTrapAllowedTime = Time.time + trapCooldown;

        // 데미지
        if (damage > 0 && health != null)
            health.TakeDamage(damage);

        // (옵션) 죽었으면 onDied가 start로 보내게 둠
        // Health에 CurrentHP가 없으면 이 부분은 생략해도 됨
        // if (health != null && health.CurrentHP <= 0) return;

        // 방향 보정
        lastMoveDir.y = 0f;
        if (lastMoveDir.sqrMagnitude < 1e-6f)
            lastMoveDir = transform.forward;
        lastMoveDir.Normalize();

        // 후보 위치
        Vector3 basePos = transform.position;
        Vector3 candidate = basePos - lastMoveDir * backDistance;
        candidate.y += upOffset;

        // ✅ 벽/오브젝트 안이면 안전한 위치로 보정
        Vector3 safe = FindNonOverlappingPosition(basePos, candidate, 6);

        TeleportToRaw(safe);
    }

    // 후보 위치가 막히면 from~to 사이에서 빈 위치를 찾음
    private Vector3 FindNonOverlappingPosition(Vector3 from, Vector3 to, int steps)
    {
        if (!IsOverlappingPlayerCapsule(to)) return to;

        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector3 p = Vector3.Lerp(from, to, t);
            if (!IsOverlappingPlayerCapsule(p))
                return p;
        }

        // 끝까지 막히면 lastSafePoint로 fallback (원하면 from으로 바꿔도 됨)
        if (lastSafePoint != Vector3.zero && !IsOverlappingPlayerCapsule(lastSafePoint))
            return lastSafePoint;

        return from;
    }

    private bool IsOverlappingPlayerCapsule(Vector3 pos)
    {
        Vector3 bottom = pos + Vector3.up * playerCheckRadius;
        Vector3 top = pos + Vector3.up * (playerCheckHeight - playerCheckRadius);

        return Physics.CheckCapsule(
            bottom,
            top,
            playerCheckRadius,
            solidMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private void TeleportToRaw(Vector3 p)
    {
        if (resetVelocity && rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        transform.position = p;
        Physics.SyncTransforms();
    }

    private void HandleDied()
    {
        if (respawnCo != null) StopCoroutine(respawnCo);
        respawnCo = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return null;

        TeleportTo(startRespawnPoint);
        health.ReviveFull();

        respawnCo = null;
    }

    private void TeleportTo(Vector3 p)
    {
        if (resetVelocity && rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        transform.position = p + Vector3.up * teleportUpOffset;
        Physics.SyncTransforms();
    }
}
