using UnityEngine;

namespace Dungeon.Player;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerAnimationDriver : MonoBehaviour
{
    [Header("Refs (auto if null)")]
    [SerializeField] private Rigidbody playerRb;
    [SerializeField] private KnockbackReceiver knockback;
    [SerializeField] private PlayerMoveDirectionTracker dirTracker;

    [Header("Tuning")]
    [SerializeField] private float runThreshold = 0.1f; // Speed 파라미터 전환 기준
    [SerializeField] private bool flipByX = true;       // x축 기준 좌우 반전

    private Animator anim;
    private SpriteRenderer sr;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int KnockbackHash = Animator.StringToHash("Knockback");

    private void Awake()
    {
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();

        // Visual은 Player의 자식이므로, 기본은 부모에서 찾아오면 편함
        if (playerRb == null) playerRb = GetComponentInParent<Rigidbody>();
        if (knockback == null) knockback = GetComponentInParent<KnockbackReceiver>();
        if (dirTracker == null) dirTracker = GetComponentInParent<PlayerMoveDirectionTracker>();
    }

    private void Update()
    {
        // 1) Speed = 현재 수평 속도(플레이어 이동/정지 판단)
        float speed = 0f;
        if (playerRb != null)
        {
            Vector3 v = playerRb.linearVelocity; // Unity 6 기준
            v.y = 0f;
            speed = v.magnitude;
        }
        anim.SetFloat(SpeedHash, speed);

        // 2) Knockback 상태
        bool isKb = knockback != null && knockback.IsKnockbacking;
        anim.SetBool(KnockbackHash, isKb);

        // 3) 방향(좌우 반전)
        if (flipByX && dirTracker != null)
        {
            float x = dirTracker.LastMoveDir.x;
            if (Mathf.Abs(x) > 0.001f)
                sr.flipX = x < 0f;
        }
    }
}
