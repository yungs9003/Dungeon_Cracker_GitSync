using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class RollingBallTrap : MonoBehaviour
{
    [Header("Lifetime (0이면 안 사라짐)")]
    [SerializeField] private float lifeTime = 4f;

    [Header("Damage")]
    [SerializeField] private int damage = 2;
    [SerializeField] private float hitCooldownPerTarget = 0.4f;

    [Header("Movement (Flat ground support)")]
    [SerializeField] private float accel = 25f;
    [SerializeField] private float stopSpeedThreshold = 0.25f;
    [SerializeField] private float stuckTimeToStop = 0.6f;

    [Header("Obstacle check (맵/벽만)")]
    [SerializeField] private float obstacleCheckDistance = 0.25f;
    [SerializeField] private float obstacleCheckRadius = 0.25f;
    [SerializeField] private LayerMask obstacleMask = ~0; // 벽/맵 레이어
    [SerializeField] private LayerMask playerMask = 0;    // Player 레이어 지정

    [Header("Physics")]
    [SerializeField] private bool useGravity = true;
    [SerializeField] private float extraTorque = 0f;

    [Header("Player collision ignore")]
    [SerializeField] private bool ignorePlayerPhysicsCollision = true;

    [Header("Knockback (Player)")] 
    [SerializeField] private float knockbackDistance = 1.2f;
    [SerializeField] private float knockbackDuration = 0.12f;

    private Rigidbody rb;
    private Collider myCol;
    private Collider ownerCol;

    private Vector3 moveDir;
    private float moveSpeed;
    private bool launched;
    private bool stopped;
    private float stuckTimer;

    private readonly Dictionary<Health, float> lastHitTime = new();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        myCol = GetComponent<Collider>();

        rb.useGravity = useGravity;
        rb.constraints = RigidbodyConstraints.None;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void Launch(Vector3 dir, float speed, int damage, Collider owner = null)
    {
        this.damage = damage;
        ownerCol = owner;

        // 발사 주체(launcher) 충돌 무시
        if (ownerCol != null)
            Physics.IgnoreCollision(myCol, ownerCol, true);

        dir.y = 0f;
        moveDir = dir.sqrMagnitude > 0f ? dir.normalized : transform.forward;

        moveSpeed = Mathf.Max(0f, speed);
        launched = true;
        stopped = false;
        stuckTimer = 0f;

        rb.isKinematic = false;
        rb.linearVelocity = moveDir * moveSpeed;

        // ✅ 플레이어와의 "물리 충돌"을 무시하도록 선 설정
        if (ignorePlayerPhysicsCollision)
            IgnorePlayerCollisions();

        if (lifeTime > 0f)
        {
            CancelInvoke(nameof(Despawn));
            Invoke(nameof(Despawn), lifeTime);
        }
    }

    // ✅ 플레이어 레이어에 속한 모든 Collider와 충돌 무시
    private void IgnorePlayerCollisions()
    {
        if (playerMask.value == 0)
        {
            Debug.LogWarning("[RollingBallTrap] playerMask가 0입니다. Player 레이어를 지정해줘.");
            return;
        }

        // 씬의 모든 Collider를 훑어 Player 레이어만 IgnoreCollision
        // (플레이어가 여러 콜라이더를 가질 수 있어서 GetComponent 하나로는 부족할 수 있음)
        Collider[] myCols = GetComponentsInChildren<Collider>(true);
        Collider[] all = FindObjectsOfType<Collider>();
        for (int i = 0; i < all.Length; i++)
        {
            Collider c = all[i];
            if (c == null) continue;

            int layerBit = 1 << c.gameObject.layer;
            if ((playerMask.value & layerBit) == 0) continue;

            foreach (var mc in myCols)
            {
                // ✅ 트리거는 충돌 무시해도 Trigger 이벤트가 안 들어올 수 있으니,
                // "월드용(NonTrigger) 콜라이더만" Ignore 하는 게 안전함
                if (mc == null) continue;
                if (mc.isTrigger) continue;  // ⭐ 트리거 콜라이더는 ignore하지 말자
                Physics.IgnoreCollision(mc, c, true);
            }
        }
    }

    private void FixedUpdate()
    {
        if (!launched || stopped) return;

        // 1) 목표 속도 유지
        Vector3 v = rb.linearVelocity;
        Vector3 flatV = new Vector3(v.x, 0f, v.z);
        Vector3 targetV = moveDir * moveSpeed;
        Vector3 dv = targetV - flatV;
        rb.AddForce(dv * accel, ForceMode.Acceleration);

        if (extraTorque > 0f)
        {
            Vector3 torqueAxis = Vector3.Cross(Vector3.up, moveDir).normalized;
            rb.AddTorque(torqueAxis * extraTorque, ForceMode.Acceleration);
        }

        // 2) 막힘 판정(플레이어 제외)
        bool slow = flatV.magnitude <= stopSpeedThreshold;
        bool blockedByWorld = false;

        if (slow && IsGrounded())
        {
            Vector3 origin = rb.worldCenterOfMass;

            if (Physics.SphereCast(origin, obstacleCheckRadius, moveDir, out RaycastHit hit,
                obstacleCheckDistance, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                bool isPlayer = (playerMask.value != 0) && ((playerMask.value & (1 << hit.collider.gameObject.layer)) != 0);
                if (!isPlayer)
                    blockedByWorld = Vector3.Dot(hit.normal, moveDir) < -0.2f;
            }
        }

        if (blockedByWorld)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= stuckTimeToStop)
                StopMoving();
        }
        else stuckTimer = 0f;
    }

    private bool IsGrounded()
    {
        Vector3 origin = rb.worldCenterOfMass;
        float groundCheckDist = obstacleCheckRadius + 0.08f;
        return Physics.SphereCast(origin, obstacleCheckRadius * 0.9f, Vector3.down,
            out _, groundCheckDist, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    private void StopMoving()
    {
        stopped = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
    }

    // ✅ 플레이어는 Trigger로 데미지 처리 (물리 힘 없음)
    private void OnTriggerEnter(Collider other) => TryDamageAndKnockback(other);
    private void OnTriggerStay(Collider other)  => TryDamageAndKnockback(other);
    // (선택) 혹시라도 플레이어와 Collision 이벤트가 들어오면 무시
    private void OnCollisionEnter(Collision collision)
    {
        if (IsPlayerLayer(collision.collider.gameObject.layer)) return;
        // 필요하면 맵/벽 충돌 로직 추가 가능
    }

    private void OnCollisionStay(Collision collision)
    {
        if (IsPlayerLayer(collision.collider.gameObject.layer)) return;
    }

    private bool IsPlayerLayer(int layer)
    {
        if (playerMask.value == 0) return false;
        return (playerMask.value & (1 << layer)) != 0;
    }

    private void TryDamageAndKnockback(Collider other)
{
    if (other.isTrigger) return;
    // 플레이어만 처리하고 싶으면 이 줄을 켜줘
    if (!IsPlayerLayer(other.gameObject.layer)) return;

    if (ownerCol != null && other == ownerCol) return;

    Health health = other.GetComponentInParent<Health>();
    if (health == null) return;

    float now = Time.time;
    if (lastHitTime.TryGetValue(health, out float t) && now - t < hitCooldownPerTarget)
        return;

    lastHitTime[health] = now;
    health.TakeDamage(damage);

    // ✅ 넉백: 공에서 플레이어로 향하는 방향으로 밀기(=플레이어가 공에서 멀어짐)
    var kb = other.GetComponentInParent<KnockbackReceiver>();
    if (kb != null)
    {
        Vector3 away = (other.transform.position - transform.position);
        away.y = 0f;
        kb.AddKnockback(away, knockbackDistance, knockbackDuration);
    }
}

    private void Despawn() => Destroy(gameObject);
}
