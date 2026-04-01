using UnityEngine;
using Dungeon.Player;

namespace Dungeon.Traps
{
    [RequireComponent(typeof(Collider2D))]
    public class PitTile2D : MonoBehaviour
    {
        [SerializeField] private int damage = 1;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var respawn = other.GetComponentInParent<Respawn2D>();
            if (respawn == null) return;

            respawn.ReturnToLastSafePointFromTrap(damage);
        }
    }
}