using UnityEngine;

namespace Dungeon.Player
{
    public class PlayerAnimationDriver2D : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D targetRb;
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [SerializeField] private string speedParam = "Speed";

        private void Awake()
        {
            if (targetRb == null)
                targetRb = GetComponentInParent<Rigidbody2D>();

            if (animator == null)
                animator = GetComponent<Animator>();

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (targetRb == null || animator == null) return;

            Vector2 v = targetRb.linearVelocity;
            animator.SetFloat(speedParam, v.magnitude);

            if (spriteRenderer != null)
            {
                if (v.x > 0.01f) spriteRenderer.flipX = false;
                else if (v.x < -0.01f) spriteRenderer.flipX = true;
            }
        }
    }
}