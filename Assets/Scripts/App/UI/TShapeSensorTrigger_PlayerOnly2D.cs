using UnityEngine;

namespace Dungeon.Traps
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class TShapeSensorTrigger_PlayerOnly2D : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField] private float cellSize = 1f;

        [Header("T Shape")]
        [SerializeField] private int forwardCells = 3;
        [SerializeField] private int sideAtForward = 1;
        [SerializeField] private int sideOffset = 1;

        [Header("Collider")]
        [SerializeField] private float thickness = 1f;
        [SerializeField] private float padding = 0.05f;

        [Header("Trigger Target (Player Only)")]
        [SerializeField] private bool useLayerCheck = true;
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private string playerTag = "Player";

        [Header("Trap")]
        [SerializeField] private WallSpikeTrap2D trap;

        private void Reset()
        {
            var rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
        }

        private void Awake()
        {
            var rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
            BuildTShape();
        }

        [ContextMenu("Rebuild T-Shape")]
        public void BuildTShape()
        {
            foreach (var bc in GetComponents<BoxCollider2D>())
                DestroyImmediate(bc);

            // ✅ 전방 기둥: 이제 +Y 방향이 기본 전방
            var colForward = gameObject.AddComponent<BoxCollider2D>();
            colForward.isTrigger = true;

            float yCenter = ((forwardCells + 1) * 0.5f) * cellSize;
            colForward.offset = new Vector2(0f, yCenter);
            colForward.size = new Vector2(thickness * cellSize - padding, forwardCells * cellSize - padding);

            // ✅ 좌/우 팔: 전방 1칸 지점에서 X축 좌우
            var colLeft = gameObject.AddComponent<BoxCollider2D>();
            colLeft.isTrigger = true;
            colLeft.offset = new Vector2(-sideOffset * cellSize, sideAtForward * cellSize);
            colLeft.size = new Vector2(1f * cellSize - padding, 1f * cellSize - padding);

            var colRight = gameObject.AddComponent<BoxCollider2D>();
            colRight.isTrigger = true;
            colRight.offset = new Vector2(+sideOffset * cellSize, sideAtForward * cellSize);
            colRight.size = new Vector2(1f * cellSize - padding, 1f * cellSize - padding);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            if (trap != null)
                trap.TriggerOnce();
        }

        private bool IsPlayer(Collider2D other)
        {
            if (useLayerCheck)
                return (playerLayer.value & (1 << other.gameObject.layer)) != 0;

            return other.CompareTag(playerTag) || other.GetComponentInParent<Transform>()?.CompareTag(playerTag) == true;
        }
    }
}