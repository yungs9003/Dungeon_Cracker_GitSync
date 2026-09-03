using UnityEngine;
using UnityEngine.SceneManagement;
using Dungeon.Core;

namespace Dungeon.Manager
{
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        [Header("Scene Names")]
        [SerializeField] private string defaultCheckpointSceneName = "CheckpointScene";
        [SerializeField] private string clearSceneName = "ClearScene";

        private int pendingMapIndex = -1;
        private string pendingReturnSceneName = "";

        public bool HasCheckpoint
        {
            get
            {
                StatusSaveData data = GetData();
                return data != null
                       && data.checkpoint != null
                       && data.checkpoint.resumeRequested
                       && !string.IsNullOrEmpty(data.checkpoint.sceneName)
                       && data.checkpoint.mapIndex >= 0;
            }
        }

        public bool HasActiveCheckpoint => HasCheckpoint;

        public string CheckpointSceneName => defaultCheckpointSceneName;

        public int PendingMapIndex => pendingMapIndex;
        public string PendingReturnSceneName => pendingReturnSceneName;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (transform.parent != null)
                transform.SetParent(null);

            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            EnsureCheckpointData();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Instance = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == clearSceneName)
                ClearCheckpoint();
        }

        private StatusSaveFile GetSaveFile()
        {
            if (StatusSaveFile.Instance != null)
                return StatusSaveFile.Instance;

            return FindFirstObjectByType<StatusSaveFile>();
        }

        private StatusSaveData GetData()
        {
            StatusSaveFile saveFile = GetSaveFile();

            if (saveFile == null)
            {
                Debug.LogWarning("[SaveManager] StatusSaveFile.Instance is missing.");
                return null;
            }

            return saveFile.Data;
        }

        private void EnsureCheckpointData()
        {
            StatusSaveData data = GetData();

            if (data == null)
                return;

            if (data.checkpoint == null)
                data.checkpoint = new CheckpointData();
        }

        private void SaveStatusFile()
        {
            StatusSaveFile saveFile = GetSaveFile();

            if (saveFile == null)
            {
                Debug.LogWarning("[SaveManager] Save failed. StatusSaveFile.Instance is missing.");
                return;
            }

            saveFile.Save();
        }

        public void SetCheckpoint(string returnSceneName, int nextMapIndex, string checkpointSceneName = "")
        {
            EnsureCheckpointData();

            StatusSaveData data = GetData();

            if (data == null || data.checkpoint == null)
                return;

            if (string.IsNullOrEmpty(returnSceneName))
            {
                Debug.LogWarning("[SaveManager] SetCheckpoint failed: returnSceneName is empty.");
                return;
            }

            data.checkpoint.sceneName = returnSceneName;
            data.checkpoint.mapIndex = Mathf.Max(0, nextMapIndex);
            data.checkpoint.resumeRequested = true;
            data.checkpoint.broken_traps_snapshot = data.broken_traps;

            pendingReturnSceneName = "";
            pendingMapIndex = -1;

            SaveStatusFile();

            Debug.Log(
                $"[SaveManager] Checkpoint saved. " +
                $"CheckpointScene={defaultCheckpointSceneName}, " +
                $"ReturnScene={data.checkpoint.sceneName}, " +
                $"NextMapIndex={data.checkpoint.mapIndex}" +
                $"broken_traps={data.broken_traps}, " +
                $"snapshot={data.checkpoint.broken_traps_snapshot}"
            );
        }

        public bool LoadCheckpointSceneIfAvailable()
        {
            EnsureCheckpointData();

            StatusSaveData data = GetData();

            if (data == null || data.checkpoint == null)
                return false;
            Debug.Log(
                $"[SaveManager] LoadCheckpointSceneIfAvailable called. " +
                $"HasCheckpoint={HasCheckpoint}, " +
                $"sceneName={data.checkpoint.sceneName}, " +
                $"mapIndex={data.checkpoint.mapIndex}, " +
                $"resumeRequested={data.checkpoint.resumeRequested}, " +
                $"broken_traps={data.broken_traps}, " +
                $"snapshot={data.checkpoint.broken_traps_snapshot}"
            );
            if (!HasCheckpoint)
            {
                data.broken_traps = 0;
                data.clear_reward_claimed = false;
                data.last_getting_gold = 0;

                SaveStatusFile();
                Debug.Log($"[SaveManager] No checkpoint. Reset broken_traps to 0. " + $"Current broken_traps={data.broken_traps}");
                return false;
            }

            if (string.IsNullOrEmpty(defaultCheckpointSceneName))
            {
                Debug.LogWarning("[SaveManager] defaultCheckpointSceneName is empty.");
                return false;
            }

            data.broken_traps = Mathf.Max(0, data.checkpoint.broken_traps_snapshot);
            data.clear_reward_claimed = false;
            data.last_getting_gold = 0;

            SaveStatusFile();
            Debug.Log($"[SaveManager] Has checkpoint. Restore broken_traps to snapshot. " + $"Current broken_traps={data.broken_traps}");
            Debug.Log($"[SaveManager] Load checkpoint scene: {defaultCheckpointSceneName}");

            SceneManager.LoadScene(defaultCheckpointSceneName, LoadSceneMode.Single);
            return true;
        }

        public bool ExitCheckpointToSavedNextMap()
        {
            EnsureCheckpointData();

            StatusSaveData data = GetData();

            if (data == null || data.checkpoint == null)
                return false;

            if (!HasCheckpoint)
            {
                Debug.LogWarning("[SaveManager] No active checkpoint. Cannot exit checkpoint.");
                return false;
            }

            if (string.IsNullOrEmpty(data.checkpoint.sceneName))
            {
                Debug.LogWarning("[SaveManager] checkpoint.sceneName is empty.");
                return false;
            }

            pendingReturnSceneName = data.checkpoint.sceneName;
            pendingMapIndex = Mathf.Max(0, data.checkpoint.mapIndex);

            Debug.Log(
                $"[SaveManager] Exit checkpoint. " +
                $"ReturnScene={pendingReturnSceneName}, PendingMapIndex={pendingMapIndex}"
            );

            SceneManager.LoadScene(pendingReturnSceneName, LoadSceneMode.Single);
            return true;
        }

        public bool TryConsumePendingMapIndex(string sceneName, out int mapIndex)
        {
            mapIndex = -1;

            if (pendingMapIndex < 0)
                return false;

            if (pendingReturnSceneName != sceneName)
                return false;

            mapIndex = pendingMapIndex;

            pendingReturnSceneName = "";
            pendingMapIndex = -1;

            Debug.Log($"[SaveManager] Consumed pending map index: {mapIndex} for scene {sceneName}");
            return true;
        }

        public void ClearCheckpoint()
        {
            EnsureCheckpointData();

            StatusSaveData data = GetData();

            if (data == null || data.checkpoint == null)
                return;

            data.checkpoint.sceneName = "";
            data.checkpoint.mapIndex = -1;
            data.checkpoint.resumeRequested = false;

            pendingReturnSceneName = "";
            pendingMapIndex = -1;

            SaveStatusFile();

            Debug.Log("[SaveManager] Checkpoint cleared.");
        }

        public void RestartDungeonFromFirstMap(string dungeonSceneName)
        {
            ClearCheckpoint();

            if (string.IsNullOrEmpty(dungeonSceneName))
            {
                Debug.LogWarning("[SaveManager] RestartDungeonFromFirstMap failed: dungeonSceneName is empty.");
                return;
            }

            Debug.Log($"[SaveManager] Restart dungeon from first map. Scene={dungeonSceneName}");

            SceneManager.LoadScene(dungeonSceneName, LoadSceneMode.Single);
        }
    }
}
