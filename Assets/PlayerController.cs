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

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<Health>();
        speed = GetComponent<SpeedController>();
        knockback = GetComponent<KnockbackReceiver>();

        rb.freezeRotation = true;
    }

    public void OnMove(InputValue value) => moveInput = value.Get<Vector2>();

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        Vector3 dir = new Vector3(moveInput.x, 0f, moveInput.y);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        Vector3 velocity = dir * speed.CurrentSpeed;
        Vector3 kbDisp = knockback != null ? knockback.ConsumeDisplacement(dt) : Vector3.zero;

        rb.MovePosition(rb.position + velocity * dt + kbDisp);
    }
}
