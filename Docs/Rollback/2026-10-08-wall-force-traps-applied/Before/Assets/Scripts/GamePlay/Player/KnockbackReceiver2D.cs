using UnityEngine;

namespace Dungeon.Player
{
    public class KnockbackReceiver2D : MonoBehaviour
    {
        private Vector2 kbVel;
        private float kbTimeLeft;

        public bool IsKnockbacking => kbTimeLeft > 0f;

        /// <summary>
        /// dir: 밀리는 방향(normalized 권장)
        /// distance: 총 밀릴 거리
        /// duration: 밀리는 시간
        /// </summary>
        public void AddKnockback(Vector2 dir, float distance, float duration)
        {
            if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize();

            duration = Mathf.Max(0.01f, duration);
            kbVel = dir * (distance / duration);
            kbTimeLeft = duration;
        }

        /// <summary>
        /// PlayerController2D가 FixedUpdate에서 호출해
        /// 이번 프레임 넉백 이동량을 받아가면 됨.
        /// </summary>
        public Vector2 ConsumeDisplacement(float dt)
        {
            if (kbTimeLeft <= 0f) return Vector2.zero;

            float step = Mathf.Min(dt, kbTimeLeft);
            kbTimeLeft -= step;

            return kbVel * step;
        }

        public void CancelKnockback()
        {
            kbVel = Vector2.zero;
            kbTimeLeft = 0f;
        }
    }
}