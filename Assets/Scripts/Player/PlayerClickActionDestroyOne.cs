using UnityEngine;
using UnityEngine.InputSystem;

namespace Dungeon.Player;

public class PlayerClickActionDestroyOne : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ForwardZoneCollector zone;
    [SerializeField] private Transform 기준점; // 거리 계산 기준(보통 플레이어)

    [Header("Optional click rate limit")]
    [SerializeField] private float clickCooldown = 0.08f;
    private float nextTime;

    private void Awake()
    {
        if (기준점 == null) 기준점 = transform;

        // zone을 실수로 안 넣었을 때를 대비(플레이어 자식/부모에서 탐색)
        if (zone == null)
            zone = GetComponentInChildren<ForwardZoneCollector>();
    }

    // ✅ Send Messages 대응 버전 (PlayerInput Behavior = Send Messages)
    public void OnAction(InputValue value)
    {
        // 버튼은 눌림(true)일 때만 처리
        if (!value.isPressed) return;
        HandleAction();
    }

    // ✅ (선택) Unity Events / C# Events에서도 그대로 쓰고 싶다면 유지
    public void OnAction(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        HandleAction();
    }

    private void HandleAction()
    {
        if (zone == null) return;

        // 클릭 연타 제한(원치 않으면 clickCooldown=0으로)
        if (clickCooldown > 0f && Time.time < nextTime) return;
        nextTime = Time.time + clickCooldown;

        // 혹시 리스트에 null이 남아있으면 정리
        zone.projectiles.RemoveAll(p => p == null);
        zone.destructibles.RemoveAll(d => d == null);

        // 1) 투사체가 있으면 투사체 1개만 제거
        if (TryDestroyNearestProjectile()) return;

        // 2) 투사체가 없으면 Destructible 1개 제거
        TryDestroyNearestDestructible();
    }

    private bool TryDestroyNearestProjectile()
    {
        if (zone.projectiles == null || zone.projectiles.Count == 0) return false;

        MonoBehaviour best = null;
        float bestDist = float.PositiveInfinity;

        foreach (var mb in zone.projectiles)
        {
            if (mb == null) continue;

            float d = (mb.transform.position - 기준점.position).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = mb;
            }
        }

        if (best == null) return false;

        // ✅ 1클릭 1파괴 보장: 먼저 목록에서 제거(중복 파괴 방지)
        zone.projectiles.Remove(best);

        // ✅ 인터페이스로 파괴 호출 (Arrow 등)
        if (best is IPlayerDestroyableProjectile p)
        {
            p.DestroyByPlayer();
            return true;
        }

        // 안전장치: 인터페이스가 없으면 아무것도 안 하고 false
        return false;
    }

    private bool TryDestroyNearestDestructible()
    {
        if (zone.destructibles == null || zone.destructibles.Count == 0) return false;

        Destructible best = null;
        float bestDist = float.PositiveInfinity;

        foreach (var d in zone.destructibles)
        {
            if (d == null) continue;

            float dist = (d.transform.position - 기준점.position).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = d;
            }
        }

        if (best == null) return false;

        // ✅ 먼저 제거해서 중복 파괴 방지
        zone.destructibles.Remove(best);

        // ✅ 함정 비활성화 + 블록 삭제 로직(Destructible에 구현)
        best.ExecuteDestruction();
        return true;
    }
}