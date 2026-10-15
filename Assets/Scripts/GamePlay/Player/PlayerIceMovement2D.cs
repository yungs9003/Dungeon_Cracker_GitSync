using Dungeon.Traps;
using UnityEngine;

namespace Dungeon.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerIceMovement2D : MonoBehaviour
    {
        private Rigidbody2D body;
        private Vector2 velocity, lastPosition, lastExternalVelocity;
        private bool wasOnIce, hasStep;
        public Vector2 SlidingVelocity => velocity;
        public bool IsOnIce => wasOnIce;
        private void Awake() => body = GetComponent<Rigidbody2D>();
        public Vector2 CalculateVelocity(Vector2 input, float normalSpeed, float dt, Vector2 externalDisplacement)
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            dt = Mathf.Max(.0001f, dt);
            normalSpeed = Mathf.Max(0f, normalSpeed);
            if (!IceFloorTrap2D.TryGetSurface(body.worldCenterOfMass, out var surface))
            {
                wasOnIce = false; hasStep = false; velocity = input * normalSpeed;
                return velocity;
            }
            input = Vector2.ClampMagnitude(input, 1f);
            if (wasOnIce && hasStep)
            {
                Vector2 actual = (body.position - lastPosition) / dt - lastExternalVelocity;
                // 충돌로 막힌 축에 가속을 쌓지 않습니다. 큰 위치 변화는 순간 이동으로 취급합니다.
                if (actual.magnitude > normalSpeed * 1.5f + 5f) velocity = Vector2.zero;
                else
                {
                    if (Mathf.Abs(actual.x) < Mathf.Abs(velocity.x)) velocity.x = actual.x;
                    if (Mathf.Abs(actual.y) < Mathf.Abs(velocity.y)) velocity.y = actual.y;
                }
            }
            float maxSpeed = normalSpeed * surface.MaxSpeedMultiplier;
            velocity = Vector2.ClampMagnitude(velocity, maxSpeed);
            float rate = input.sqrMagnitude < .0001f ? surface.ReleaseDeceleration
                : Vector2.Dot(velocity, input) < 0f ? surface.ReverseBraking : surface.Acceleration;
            velocity = Vector2.MoveTowards(velocity, input * maxSpeed, normalSpeed * rate * dt);
            wasOnIce = true; hasStep = true; lastPosition = body.position;
            lastExternalVelocity = externalDisplacement / dt;
            return velocity;
        }
        public void ResetMotion() { velocity = Vector2.zero; wasOnIce = false; hasStep = false; lastExternalVelocity = Vector2.zero; }
        private void OnDisable() => ResetMotion();
    }
}
