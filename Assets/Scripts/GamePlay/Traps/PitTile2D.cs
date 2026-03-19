using UnityEngine;
using Dungeon.Player;

namespace Dungeon.Traps
{
    [RequireComponent(typeof(Collider2D))]
    public class PitTile2D : MonoBehaviour
    {
        [SerializeField] private int damage = 1;
        [SerializeField] private float backDistance = 1.0f;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var respawn = other.GetComponentInParent<Respawn2D>();
            if (respawn == null) return;

            var tracker = other.GetComponentInParent<PlayerMoveDirectionTracker2D>();
            Vector2 dir = tracker != null ? tracker.LastMoveDir : Vector2.down;

            respawn.TrapReturnByDirection(damage, dir, backDistance);
        }
    }
}