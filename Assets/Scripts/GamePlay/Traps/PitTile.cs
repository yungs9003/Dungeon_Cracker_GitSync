using UnityEngine;
using Dungeon.Player;

namespace Dungeon.Traps
{
public class PitTile : MonoBehaviour
{
    [SerializeField] private float backDistance = 1.0f; // 1칸 정도
    [SerializeField] private float upOffset = 0.3f;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        var respawn = other.GetComponentInParent<Respawn>();
        if (respawn == null) return;

        var tracker = other.GetComponentInParent<PlayerMoveDirectionTracker>();
        Vector3 dir = tracker != null ? tracker.LastMoveDir : other.transform.forward;

        Health health = other.GetComponentInParent<Health>();
        TrapDamageEvent.Raise(gameObject, health);

        if (health == null || health.currentHP > 0)
            respawn.TrapReturnByDirection(0, dir, backDistance, upOffset);
    }
}
}
