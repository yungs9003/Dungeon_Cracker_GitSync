using UnityEngine;
using UnityEngine.SceneManagement;
using Dungeon.Player;
using Dungeon.Core;

namespace Dungeon.Checkpoint
{
    [RequireComponent(typeof(Collider2D))]
    public class CheckpointExit2D : MonoBehaviour
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

            SaveFileManager.RequestCheckpointResume();

            string sceneName = SaveFileManager.GetCheckpointSceneName();

            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("[CheckpointExit2D] 저장된 던전 Scene 이름이 없습니다.");
                return;
            }

            SceneManager.LoadScene(sceneName);
        }
    }
}