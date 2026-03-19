using UnityEngine;

namespace Dungeon.Player
{
    public class PlayerMoveDirectionTracker2D : MonoBehaviour
    {
        // 최근 유효 이동 방향(2D). 초기값은 아래쪽으로 둠.
        public Vector2 LastMoveDir { get; private set; } = Vector2.down;

        [SerializeField] private float minSqrMagnitude = 0.0004f; // 약 0.02^2

        public void SetMoveDir(Vector2 dir)
        {
            if (dir.sqrMagnitude < minSqrMagnitude) return;
            LastMoveDir = dir.normalized;
        }
    }
}