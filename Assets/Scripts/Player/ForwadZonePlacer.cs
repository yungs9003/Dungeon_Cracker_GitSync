using UnityEngine;

namespace Dungeon.Player;

[RequireComponent(typeof(BoxCollider))]
public class ForwardZonePlacer : MonoBehaviour
{
    [SerializeField] private PlayerMoveDirectionTracker dirTracker;

    [Header("Zone Size (XZ)")]
    [SerializeField] private float width = 2f;    // 좌우 폭 (X)
    [SerializeField] private float length = 3f;   // 전방 길이 (Z)

    [Header("Zone Height")]
    [SerializeField] private float height = 1f;   // Y 높이 (투사체 높이 커버)

    [Header("Forward Offset")]
    [SerializeField] private float forwardOffset = 1.5f; // 보통 length/2 권장

    private BoxCollider box;

    private void Awake()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(width, height, length);
        box.center = Vector3.zero;

        if (dirTracker == null)
            dirTracker = GetComponentInParent<PlayerMoveDirectionTracker>();
    }

    private void LateUpdate()
    {
        if (dirTracker == null) return;

        Vector3 dir = dirTracker.LastMoveDir;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;

        // 전방으로 밀기
        transform.localPosition = dir * forwardOffset;

        // 탑다운(XZ) => Y축 회전만
        float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }
}