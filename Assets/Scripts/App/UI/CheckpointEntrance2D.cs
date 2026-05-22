using UnityEngine;
using UnityEngine.SceneManagement;
using Dungeon.Manager;

namespace Dungeon.UI
{
    [RequireComponent(typeof(Collider2D))]
    public class CheckpointEntrance2D : MonoBehaviour
    {
        [Header("Checkpoint")]
        [SerializeField] private string checkpointSceneName = "CheckpointScene";

        [Header("Player Check")]
        [SerializeField] private string playerTag = "Player";
        [Tooltip("플레이어 중심이 체크포인트 입구 중심으로부터 이 거리 이내에 들어오면 발동합니다.")]
        [SerializeField] private float reachRadius = 0.25f;

        private bool triggered;

        private void Reset()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (triggered) return;
            if (!other.CompareTag(playerTag)) return;

            if (!IsPlayerCenterInside(other.transform)) return;

            triggered = true;
            EnterCheckpointScene();
        }

        private bool IsPlayerCenterInside(Transform playerTransform)
        {
            Vector2 playerCenter = playerTransform.position;
            Vector2 entranceCenter = transform.position;
            return Vector2.Distance(playerCenter, entranceCenter) <= reachRadius;
        }

        private void EnterCheckpointScene()
        {
            string returnSceneName = SceneManager.GetActiveScene().name;

            MapSequenceManager2D manager = MapSequenceManager2D.Instance;
            if (manager == null)
                manager = FindFirstObjectByType<MapSequenceManager2D>();

            int nextMapIndex = 0;
            if (manager != null)
                nextMapIndex = manager.GetNextMapIndexForCheckpoint();
            else
                Debug.LogWarning("[CheckpointEntrance2D] MapSequenceManager2D를 찾지 못했습니다. nextMapIndex를 0으로 저장합니다.");

            if (SaveManager.Instance != null)
                SaveManager.Instance.SetCheckpoint(returnSceneName, nextMapIndex, checkpointSceneName);
            else
                Debug.LogWarning("[CheckpointEntrance2D] SaveManager.Instance가 없습니다. 체크포인트 저장 없이 씬만 이동합니다.");

            SceneManager.LoadScene(checkpointSceneName, LoadSceneMode.Single);
        }
    }
}
