using UnityEngine;
using Dungeon.Player;
using Dungeon.Core;

namespace Dungeon.Traps
{
    [RequireComponent(typeof(Collider2D))]
    public class PitTile2D : MonoBehaviour
    {
        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (PlayerEffectLock2D.IsProtectedTarget(other)) return;
            var respawn = other.GetComponentInParent<Respawn2D>();
            if (respawn == null) return;

            Health health = other.GetComponentInParent<Health>();
            TrapDamageEvent.Raise(gameObject, health);

            if (health == null || health.currentHP > 0)
                respawn.ReturnToLastSafePointFromTrap();
        }
    }
}
