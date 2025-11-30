using UnityEngine;
using UnityEngine.InputSystem;  // 꼭 있어야 함

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("이동 속도")]
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody rb;
    private Vector2 moveInput;   // WASD/스틱 입력

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // 넘어지지 않게 회전만 고정
        rb.freezeRotation = true;
    }

    // ✅ PlayerInput (Behavior = Send Messages) 가 호출하는 함수 형태
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
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
