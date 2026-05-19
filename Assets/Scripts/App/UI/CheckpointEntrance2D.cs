using UnityEngine;
using Dungeon.Player;
using Dungeon.Manager;

namespace Dungeon.Checkpoint
{
    [RequireComponent(typeof(Collider2D))]
    public class CheckpointEntrance2D : MonoBehaviour
    {
        private bool used;

        private void Reset()
        {
            Collider2D col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        private void Awake()
        {
            Collider2D col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (used) return;

            PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
            if (player == null) return;

            used = true;

            if (MapSequenceManager2D.Instance == null)
            {
                Debug.LogWarning("[CheckpointEntrance2D] MapSequenceManager2D.Instance가 없습니다.");
                return;
            }

            MapSequenceManager2D.Instance.EnterCheckpoint(player);
        }
    }
}