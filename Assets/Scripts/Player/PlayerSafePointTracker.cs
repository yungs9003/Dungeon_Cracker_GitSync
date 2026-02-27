using UnityEngine;

namespace Dungeon.Player;

public class PlayerSafePointTracker : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Respawn respawn;

    [Header("Grid")]
    [SerializeField] private float cellSize = 1f;   // JsonBlockBuilder3D.cellSize와 동일
    [SerializeField] private float originX = 0f;
    [SerializeField] private float originZ = 0f;
    [SerializeField] private float safeY = 1.5f;    // 복귀 높이

    [Header("Grounded check (Trigger 무시)")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private float groundCheckDistance = 0.8f;

    [Header("Hazard check (Trigger 포함)")]
    [SerializeField] private LayerMask hazardMask; // 100-000(또는 PitTrigger) 레이어 포함
    [SerializeField] private Vector3 hazardHalfExtents = new Vector3(0.45f, 0.25f, 0.45f);
    [SerializeField] private float hazardCheckY = 0.1f;

    private void Awake()
    {
        if (respawn == null) respawn = GetComponent<Respawn>();
    }

    private void FixedUpdate()
    {
        if (respawn == null) return;

        // 1) 공중이면 safe 저장하지 않음 (낭떠러지 위에서 레이가 뒤 타일 맞는 문제 차단)
        if (!IsGrounded()) return;

        // 2) 현재 플레이어 위치를 "셀"로 변환 → 셀 중심 좌표 계산
        Vector2Int cell = WorldToCell(transform.position);

        // 3) 낭떠러지/위험 셀이면 저장 금지
        if (IsHazardCell(cell)) return;

        // 4) 안전지점 저장 (항상 "현재 밟는 타일"의 정중앙)
        Vector3 safe = CellToCenter(cell);
        safe.y = safeY;
        respawn.SetLastSafePoint(safe);
    }

    private bool IsGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        return Physics.SphereCast(
            origin,
            groundCheckRadius,
            Vector3.down,
            out _,
            groundCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private Vector2Int WorldToCell(Vector3 p)
    {
        int x = Mathf.FloorToInt((p.x - originX) / cellSize);
        int z = Mathf.FloorToInt((p.z - originZ) / cellSize);
        return new Vector2Int(x, z);
    }

    private Vector3 CellToCenter(Vector2Int cell)
    {
        float cx = originX + (cell.x + 0.5f) * cellSize;
        float cz = originZ + (cell.y + 0.5f) * cellSize;
        return new Vector3(cx, 0f, cz);
    }

    private bool IsHazardCell(Vector2Int cell)
    {
        if (hazardMask.value == 0) return false;

        Vector3 p = CellToCenter(cell);
        p.y = hazardCheckY;

        return Physics.CheckBox(
            p,
            hazardHalfExtents,
            Quaternion.identity,
            hazardMask,
            QueryTriggerInteraction.Collide
        );
    }
}
