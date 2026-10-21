using UnityEngine;
using UnityEngine.InputSystem;
using Dungeon.Core;
using Dungeon.Traps;
using Dungeon.Map;

namespace Dungeon.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpeedController))]
    public class PlayerController2D : MonoBehaviour
    {
        [Header("Move")]
        [SerializeField] private float moveSpeed = 2.7f;
        [SerializeField] private bool useRawNormalizedInput = true;

        [Header("Control")]
        [SerializeField] private bool canControl = true;

        private Rigidbody2D rb;
        private SpeedController speed;
        private KnockbackReceiver2D knockback;
        private PlayerMoveDirectionTracker2D dirTracker;
        private PlayerIceMovement2D iceMovement;

        private Vector2 moveInput;

        public bool CanControl => canControl;
        public Vector2 MoveInput => moveInput;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            speed = GetComponent<SpeedController>();
            speed.SetBaseSpeed(moveSpeed);
            knockback = GetComponent<KnockbackReceiver2D>();
            dirTracker = GetComponent<PlayerMoveDirectionTracker2D>();
            iceMovement = GetComponent<PlayerIceMovement2D>();
            if (iceMovement == null) iceMovement = gameObject.AddComponent<PlayerIceMovement2D>();
        }

        private void Reset()
        {
            var body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            }
        }

        private void FixedUpdate()
        {
            if (PlayerEffectLock2D.IsProtectedTarget(this) || !rb.simulated || CliffTrap2D.IsPlayerFalling(rb))
            {
                iceMovement?.ResetMotion();
                return;
            }
            float dt = Time.fixedDeltaTime;

            Vector2 input = canControl ? moveInput : Vector2.zero;

            if (useRawNormalizedInput && input.sqrMagnitude > 1f)
                input.Normalize();

            if (dirTracker != null && input.sqrMagnitude > 0.0001f)
                dirTracker.SetMoveDir(input);

            Vector2 kbDelta = knockback != null ? knockback.ConsumeDisplacement(dt) : Vector2.zero;
            Vector2 moveVelocity;
            if (!canControl)
            {
                iceMovement?.ResetMotion();
                moveVelocity = Vector2.zero;
            }
            else moveVelocity = iceMovement != null
                ? iceMovement.CalculateVelocity(input, speed.CurrentSpeed, dt, kbDelta) : input * speed.CurrentSpeed;
            Vector2 moveDelta = moveVelocity * dt;

            Vector2 requested = rb.position + moveDelta + kbDelta;
            var area = MapArea2D.ForBody(rb);
            rb.MovePosition(area != null ? area.LimitMovement(rb, rb.position, requested) : requested);
        }

        public void SetControl(bool enabled, bool stopImmediately = false)
        {
            canControl = enabled;
            if (!enabled || stopImmediately) iceMovement?.ResetMotion();

            if (stopImmediately)
            {
                moveInput = Vector2.zero;

                if (rb != null)
                    rb.linearVelocity = Vector2.zero;
            }
        }

        private void OnDisable() => iceMovement?.ResetMotion();

        // PlayerInput - Send Messages 대응
        public void OnMove(InputValue value)
        {
            moveInput = value.Get<Vector2>();
        }

        // PlayerInput - Invoke Unity Events / C# event 대응용
        public void OnMove(InputAction.CallbackContext ctx)
        {
            moveInput = ctx.ReadValue<Vector2>();
        }
    }
}
