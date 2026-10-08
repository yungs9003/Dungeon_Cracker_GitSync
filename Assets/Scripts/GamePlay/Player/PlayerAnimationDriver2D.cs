using Dungeon.Core;
using UnityEngine;

namespace Dungeon.Player
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerAnimationDriver2D : MonoBehaviour
    {
        [Header("Refs (auto if null)")]
        [SerializeField] private Rigidbody2D playerRb;
        [SerializeField] private KnockbackReceiver2D knockback;
        [SerializeField] private PlayerMoveDirectionTracker2D dirTracker;

        [Header("Tuning")]
        [SerializeField] private float moveThreshold = 0.05f;
        [SerializeField] private bool flipByX = true;
        [SerializeField] private bool useTransformDeltaForSpeed = true;

        private Animator anim;
        private SpriteRenderer sr;

        private Vector3 lastWorldPos;

        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
        private static readonly int KnockbackHash = Animator.StringToHash("Knockback");

        private void Awake()
        {
            anim = GetComponent<Animator>();
            sr = GetComponent<SpriteRenderer>();

            if (playerRb == null) playerRb = GetComponentInParent<Rigidbody2D>();
            if (knockback == null) knockback = GetComponentInParent<KnockbackReceiver2D>();
            if (dirTracker == null) dirTracker = GetComponentInParent<PlayerMoveDirectionTracker2D>();

            lastWorldPos = transform.position;
        }

        private void Start()
        {
            // 시작 시 무조건 Idle 쪽으로 가게 초기화
            anim.SetBool(IsMovingHash, false);
            anim.SetBool(KnockbackHash, false);
        }

        private void Update()
        {
            bool isForcedMovement = PlayerEffectLock2D.IsProtectedTarget(this);
            Vector3 worldPosition = transform.position;
            Vector3 delta = worldPosition - lastWorldPos;
            // 보호 중에도 위치를 기록해 해제 직후 지난 강제 이동을 걷기로 오인하지 않습니다.
            lastWorldPos = worldPosition;
            float speed = 0f;

            if (!isForcedMovement && useTransformDeltaForSpeed)
            {
                speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            }
            else if (!isForcedMovement && playerRb != null)
            {
                speed = playerRb.linearVelocity.magnitude;
            }

            bool isMoving = speed > moveThreshold;
            bool isKb = !isForcedMovement && knockback != null && knockback.IsKnockbacking;

            anim.SetBool(IsMovingHash, isMoving);
            anim.SetBool(KnockbackHash, isKb);

            if (flipByX && dirTracker != null)
            {
                float x = dirTracker.LastMoveDir.x;
                if (Mathf.Abs(x) > 0.001f)
                    sr.flipX = x < 0f;
            }
        }
    }
}
