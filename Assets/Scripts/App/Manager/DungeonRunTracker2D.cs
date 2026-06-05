using UnityEngine;
using UnityEngine.SceneManagement;
using Dungeon.Core;

namespace Dungeon.Manager
{
    public class DungeonRunTracker2D : MonoBehaviour
    {
        public static DungeonRunTracker2D Instance { get; private set; }

        [Header("Scene Filter")]
        [SerializeField] private string dungeonSceneKeyword = "DUNGEON";
        [SerializeField] private string checkpointSceneName = "CheckpointScene";
        [SerializeField] private string clearSceneName = "ClearScene";

        [Header("Save")]
        [SerializeField] private float saveInterval = 1.0f;

        private float unsavedSeconds;
        private float saveTimer;

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
        }

        private void Update()
        {
            if (!ShouldCountTime())
                return;

            float dt = Time.deltaTime;

            unsavedSeconds += dt;
            saveTimer += dt;

            if (saveTimer >= saveInterval)
                FlushTimeToSave();
        }

        private bool ShouldCountTime()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            string sceneName = activeScene.name;

            if (string.IsNullOrEmpty(sceneName))
                return false;

            if (sceneName == checkpointSceneName)
                return false;

            if (sceneName == clearSceneName)
                return false;

            return sceneName.Contains(dungeonSceneKeyword);
        }

        public void FlushTimeToSave()
        {
            if (unsavedSeconds <= 0f)
                return;

            StatusSaveFile saveFile = StatusSaveFile.Instance;

            if (saveFile == null)
                return;

            saveFile.AddClearTime(unsavedSeconds, saveImmediately: true);

            unsavedSeconds = 0f;
            saveTimer = 0f;
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
                FlushTimeToSave();
        }

        private void OnApplicationQuit()
        {
            FlushTimeToSave();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                FlushTimeToSave();
                Instance = null;
            }
        }
    }
}