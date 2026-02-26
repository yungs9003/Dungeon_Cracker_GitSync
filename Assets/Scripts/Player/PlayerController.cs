using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(SpeedController))]
[RequireComponent(typeof(KnockbackReceiver))]
[RequireComponent(typeof(PlayerMoveDirectionTracker))] // ✅ 추가: 방향 트래커 보장
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

    public void OnMove(InputValue value)
    {
        if (!canControl)
        {
            moveInput = Vector2.zero;
            return;
        }
        moveInput = value.Get<Vector2>();
    }

    public void SetControl(bool enabled, bool stopImmediately = true)
    {
        canControl = enabled;

        if (!enabled)
        {
            moveInput = Vector2.zero;

            if (stopImmediately)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (!canControl) return;

        Vector3 dir = new Vector3(moveInput.x, 0f, moveInput.y);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        // ✅ 마지막 이동 방향 유지 (이미 잘 되어 있음)
        dirTracker.SetMoveDir(dir);

        Vector3 velocity = dir * speed.CurrentSpeed;
        Vector3 kbDisp = knockback != null ? knockback.ConsumeDisplacement(dt) : Vector3.zero;

        rb.MovePosition(rb.position + velocity * dt + kbDisp);
    }
}