using UnityEngine;

namespace Dungeon.Traps
{
    public class BallVisualRoll2D : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private Transform visual;

        [Header("Roll")]
        [SerializeField] private float radius = 0.5f;
        [SerializeField] private float rotationMultiplier = 1f;
        [SerializeField] private bool invertDirection = false;

        private void Awake()
        {
            if (rb == null)
                rb = GetComponent<Rigidbody2D>();

            if (visual == null)
                visual = transform;
        }

        private void Update()
        {
            if (rb == null || visual == null || radius <= 0f) return;

            Vector2 vel = rb.linearVelocity;
            float speed = vel.magnitude;
            if (speed < 0.001f) return;

            float angularSpeedDeg = (speed / radius) * Mathf.Rad2Deg * rotationMultiplier;
            float sign = invertDirection ? 1f : -1f;

            visual.Rotate(0f, 0f, sign * angularSpeedDeg * Time.deltaTime);
        }
    }
}