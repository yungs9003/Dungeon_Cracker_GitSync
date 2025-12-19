using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(Health), typeof(SpeedController))]
public class PlayerController : MonoBehaviour
{
    [Header("이동 속도(기본값은 SpeedController의 baseSpeed에서 설정)")]
    [SerializeField] private float _unused = 0f; // Inspector에 남겨두고 싶으면 유지, 아니면 삭제

    [Header("테스트용 자동 데미지")]
    [SerializeField] private bool enableTestDamage = true;
    [SerializeField] private float damageInterval = 3f;
    [SerializeField] private int damageAmount = 1;

    private Rigidbody rb;
    private Vector2 moveInput;
    private Health health;
    private float damageTimer;

    private SpeedController speed;   // ✅ 추가

    private KnockbackReceiver knockback;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<Health>();
        speed = GetComponent<SpeedController>(); // ✅ 추가

        rb.freezeRotation = true;
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    private void Update()
    {
        if (!enableTestDamage || health == null || health.currentHP <= 0)
            return;

        damageTimer += Time.deltaTime;

        if (damageTimer >= damageInterval)
        {
            damageTimer = 0f;
            health.TakeDamage(damageAmount);

            Debug.Log($"[TestDamage] t={Time.time:F1}s, took {damageAmount} dmg → HP {health.currentHP}/{health.maxHP}");
        }
    }

    private void FixedUpdate()
    {
        Vector3 dir = new Vector3(moveInput.x, 0f, moveInput.y);
        // FixedUpdate 안에서 dir 계산 후:
        var tracker = GetComponent<PlayerMoveDirectionTracker>();
        if (tracker != null) tracker.SetMoveDir(dir);

        if (dir.sqrMagnitude > 1f)
            dir.Normalize();

        float moveSpeed = speed.CurrentSpeed; // ✅ 여기서 현재 속도 사용
        Vector3 velocity = dir * moveSpeed;

        Vector3 kb = knockback != null ? knockback.ConsumeDisplacement(Time.fixedDeltaTime) : Vector3.zero;

        rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
    }
}
