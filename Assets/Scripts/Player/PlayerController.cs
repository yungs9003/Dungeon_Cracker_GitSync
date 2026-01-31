using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(SpeedController))]
[RequireComponent(typeof(KnockbackReceiver))]
public class PlayerController : MonoBehaviour
{
    private Rigidbody rb;
    private Vector2 moveInput;
    private Health health;
    private SpeedController speed;
    private KnockbackReceiver knockback;
    private PlayerMoveDirectionTracker dirTracker;

    public bool canControl = true;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<Health>();
        speed = GetComponent<SpeedController>();
        knockback = GetComponent<KnockbackReceiver>();

        rb.freezeRotation = true;
        dirTracker = GetComponent<PlayerMoveDirectionTracker>();
    }

    // ✅ 입력 콜백에서 잠금 처리(중요)
    public void OnMove(InputValue value)
    {
        if (!canControl)
        {
            moveInput = Vector2.zero;
            return;
        }
        moveInput = value.Get<Vector2>();
    }

    // ✅ 외부(포탈 등)에서 호출할 잠금/해제 함수
    public void SetControl(bool enabled, bool stopImmediately = true)
    {
        canControl = enabled;

        if (!enabled)
        {
            moveInput = Vector2.zero;

            if (stopImmediately)
            {
                // Unity 6 권장: linearVelocity
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

            }
        }
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // ✅ 잠겼을 때도 넉백만 적용할지 선택 가능
        // "완전히 멈춤" 원하면 아래처럼 그냥 return 하는 게 가장 확실
        if (!canControl) return;

        Vector3 dir = new Vector3(moveInput.x, 0f, moveInput.y);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        if (dirTracker != null) dirTracker.SetMoveDir(dir);

        Vector3 velocity = dir * speed.CurrentSpeed;
        Vector3 kbDisp = knockback != null ? knockback.ConsumeDisplacement(dt) : Vector3.zero;

        rb.MovePosition(rb.position + velocity * dt + kbDisp);
    }
}
