using UnityEngine;

namespace Dungeon.Player
{
public class PlayerMoveDirectionTracker : MonoBehaviour
{
    // 최근 유효 이동 방향(수평). 초기값은 forward로 둠
    public Vector3 LastMoveDir { get; private set; } = Vector3.forward;

    // 이동 입력/속도가 너무 작을 때는 갱신 안 하도록
    [SerializeField] private float minSqrMagnitude = 0.0004f; // ~0.02^2

    // 외부(Controller)에서 넣어줄 수 있게
    public void SetMoveDir(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < minSqrMagnitude) return;
        LastMoveDir = dir.normalized;
    }
}
}