using UnityEngine;

namespace Dungeon.Traps;

[RequireComponent(typeof(Rigidbody))]
public class TShapeSensorTrigger_PlayerOnly : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private float cellSize = 1f;

    [Header("T Shape")]
    [SerializeField] private int forwardCells = 3;     // 전방 기둥 길이
    [SerializeField] private int sideAtForward = 1;    // 좌/우가 붙는 전방 거리
    [SerializeField] private int sideOffset = 1;       // 좌/우 칸(보통 1칸)

    [Header("Collider Height")]
    [SerializeField] private float ySize = 2.0f;
    [SerializeField] private float yCenter = 0.5f;
    [SerializeField] private float padding = 0.05f;

    [Header("Trigger Target (Player Only)")]
    [Tooltip("Player Layer만 체크하고 싶으면 켜기")]
    [SerializeField] private bool useLayerCheck = true;

    [Tooltip("Player 레이어만 포함하도록 설정 (useLayerCheck=true일 때 사용)")]
    [SerializeField] private LayerMask playerLayer;

    [Tooltip("Player Tag로 체크하고 싶으면 지정 (useLayerCheck=false일 때 사용)")]
    [SerializeField] private string playerTag = "Player";

    [Header("Trap")]
    [SerializeField] private WallSpikeTrap trap;

    private void Reset()
    {
        var rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
    }

    private void Awake()
    {
        var rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        BuildTShape();
    }

    [ContextMenu("Rebuild T-Shape")]
    public void BuildTShape()
    {
        // 기존 BoxCollider 삭제(센서용 생성분)
        foreach (var bc in GetComponents<BoxCollider>())
            Destroy(bc);

        // 1) 전방 기둥
        var colForward = gameObject.AddComponent<BoxCollider>();
        colForward.isTrigger = true;

        float zCenter = ((forwardCells + 1) * 0.5f) * cellSize;
        colForward.center = new Vector3(0f, yCenter, zCenter);
        colForward.size   = new Vector3(1f * cellSize - padding, ySize, forwardCells * cellSize - padding);

        // 2) 좌/우 팔(전방 1칸 지점)
        var colLeft = gameObject.AddComponent<BoxCollider>();
        colLeft.isTrigger = true;
        colLeft.center = new Vector3(-sideOffset * cellSize, yCenter, sideAtForward * cellSize);
        colLeft.size   = new Vector3(1f * cellSize - padding, ySize, 1f * cellSize - padding);

        var colRight = gameObject.AddComponent<BoxCollider>();
        colRight.isTrigger = true;
        colRight.center = new Vector3(+sideOffset * cellSize, yCenter, sideAtForward * cellSize);
        colRight.size   = new Vector3(1f * cellSize - padding, ySize, 1f * cellSize - padding);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;

        if (trap != null)
            trap.TriggerOnce();
    }

    private bool IsPlayer(Collider other)
    {
        if (useLayerCheck)
        {
            // ✅ Player 레이어만 통과
            return (playerLayer.value & (1 << other.gameObject.layer)) != 0;
        }
        else
        {
            // ✅ Player 태그만 통과
            return other.CompareTag(playerTag) || other.GetComponentInParent<Transform>()?.CompareTag(playerTag) == true;
        }
    }
}
