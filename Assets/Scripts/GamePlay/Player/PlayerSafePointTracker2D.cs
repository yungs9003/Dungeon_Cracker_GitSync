using UnityEngine;
using Dungeon.Core;
using Dungeon.Traps;

namespace Dungeon.Player
{
    [RequireComponent(typeof(Respawn2D))]
    public class PlayerSafePointTracker2D : MonoBehaviour
    {
        [SerializeField] private Respawn2D respawn;

        [Header("Grid")]
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector2 safePointOffset = Vector2.zero;

        [Header("Unsafe Check")]
        [SerializeField] private LayerMask unsafeMask;
        [SerializeField] private float sampleRadius = 0.1f;

        [Header("Optimize")]
        [SerializeField] private bool updateOnlyWhenCellChanges = true;

        private Vector2Int lastCheckedCell = new Vector2Int(int.MinValue, int.MinValue);

        private void Awake()
        {
            if (respawn == null)
                respawn = GetComponent<Respawn2D>();
        }

        private void FixedUpdate()
        {
            if (PlayerEffectLock2D.IsProtectedTarget(this) || CliffTrap2D.IsPlayerFalling(GetComponent<Rigidbody2D>())) return;
            if (respawn == null || cellSize <= 0f) return;

            Vector2Int cell = WorldToCell(transform.position);

            if (updateOnlyWhenCellChanges && cell == lastCheckedCell)
                return;

            lastCheckedCell = cell;

            Vector2 safePoint = CellToWorld(cell) + safePointOffset;

            // 현재 셀이 위험 타일이면 lastSafePoint 갱신 금지
            if (unsafeMask.value != 0 &&
                Physics2D.OverlapCircle(safePoint, sampleRadius, unsafeMask) != null)
            {
                return;
            }

            respawn.SetLastSafePoint(safePoint);
        }

        private Vector2Int WorldToCell(Vector2 worldPos)
        {
            int x = Mathf.RoundToInt(worldPos.x / cellSize);
            int y = Mathf.RoundToInt(worldPos.y / cellSize);
            return new Vector2Int(x, y);
        }

        private Vector2 CellToWorld(Vector2Int cell)
        {
            return new Vector2(cell.x * cellSize, cell.y * cellSize);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (cellSize <= 0f) return;

            Vector2Int cell = WorldToCell(transform.position);
            Vector2 safePoint = CellToWorld(cell) + safePointOffset;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(safePoint, sampleRadius);
        }
#endif
    }
}
