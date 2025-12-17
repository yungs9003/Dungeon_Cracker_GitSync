using UnityEngine;
using UnityEngine.InputSystem;  // 꼭 있어야 함

// Rigidbody + Health 둘 다 반드시 존재하도록 강제
[RequireComponent(typeof(Rigidbody), typeof(Health))]
public class PlayerController : MonoBehaviour
{
    [Header("이동 속도")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("테스트용 자동 데미지")]
    [SerializeField] private bool enableTestDamage = true; // 끄고 켜기
    [SerializeField] private float damageInterval = 3f;    // 몇 초마다?
    [SerializeField] private int damageAmount = 1;         // 한 번에 몇 깎을지

    private Rigidbody rb;
    private Vector2 moveInput;   // WASD/스틱 입력
    private Health health;
    private float damageTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<Health>();

        // 넘어지지 않게 회전만 고정
        rb.freezeRotation = true;
    }

    // PlayerInput (Behavior = Send Messages)가 호출
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    private void Update()
    {
        // ✅ 테스트용 자동 데미지
        if (!enableTestDamage || health == null || health.currentHP <= 0)
            return;

        damageTimer += Time.deltaTime;

        if (damageTimer >= damageInterval)
        {
            damageTimer = 0f;

            // 실제 데미지 적용
            health.TakeDamage(damageAmount);

            // 로그 출력
            Debug.Log(
                $"[TestDamage] t={Time.time:F1}s, " +
                $"took {damageAmount} dmg → HP {health.currentHP}/{health.maxHP}");
        }
    }

    private void FixedUpdate()
    {
        // XZ 평면 방향으로 변환
        Vector3 dir = new Vector3(moveInput.x, 0f, moveInput.y);

        if (dir.sqrMagnitude > 1f)
            dir.Normalize();

        Vector3 velocity = dir * moveSpeed;
        rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
    }
}
