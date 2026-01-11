using UnityEngine;

public class KnockbackReceiver : MonoBehaviour
{
    private Vector3 kbVel;     // 넉백 속도
    private float kbTimeLeft;  // 남은 시간
    public bool IsKnockbacking => kbTimeLeft > 0f;

    /// <summary>
    /// dir: 밀리는 방향(수평 권장, normalized 권장)
    /// distance: 총 밀릴 거리
    /// duration: 밀리는 시간(짧을수록 튕기는 느낌)
    /// </summary>
    public void AddKnockback(Vector3 dir, float distance, float duration)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return;
        dir.Normalize();

        duration = Mathf.Max(0.01f, duration);
        kbVel = dir * (distance / duration);
        kbTimeLeft = duration;
    }

    /// <summary>
    /// PlayerController에서 매 FixedUpdate 호출해서 넉백 이동량을 받아가면 됨
    /// </summary>
    public Vector3 ConsumeDisplacement(float dt)
    {
        if (kbTimeLeft <= 0f) return Vector3.zero;

        float step = Mathf.Min(dt, kbTimeLeft);
        kbTimeLeft -= step;

        return kbVel * step;
    }

    public void CancelKnockback()
    {
        kbVel = Vector3.zero;
        kbTimeLeft = 0f;
    }
}
