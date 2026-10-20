using UnityEngine;
using UnityEngine.SceneManagement;
using Dungeon.Core;

namespace Dungeon.Manager
{
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }
        [SerializeField] private string defaultCheckpointSceneName = "CheckpointScene";
        [SerializeField] private string clearSceneName = "ClearScene";
        private int pendingMapIndex = -1;
        private string pendingReturnSceneName = "";
        public string CheckpointSceneName => defaultCheckpointSceneName;
        public int PendingMapIndex => pendingMapIndex;
        public string PendingReturnSceneName => pendingReturnSceneName;
        public bool HasCheckpoint => DungeonProgressService.TryGetActiveCheckpoint(StatusSaveFile.Instance?.Data, out var checkpoint) && !checkpoint.completed && checkpoint.resumeRequested;
        public bool HasActiveCheckpoint => HasCheckpoint;

        public static SaveManager GetOrCreate() => Instance != null ? Instance : new GameObject("SaveManager").AddComponent<SaveManager>();
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded; Instance = null;
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == clearSceneName) StatusSaveFile.Instance?.TryFinalizeDungeonClear();
        }
        private static void FlushTime() => StatusSaveFile.Instance?.CapturePlayTime();

        public bool EnterDungeon(string sceneName)
        {
            sceneName = DungeonProgressService.SceneKey(sceneName);
            var save = StatusSaveFile.Instance;
            if (save == null || !Application.CanStreamedLevelBeLoaded(sceneName)) return false;
            int count = DungeonMapCatalog.Load(sceneName).Length;
            if (count == 0) return false;
            if (save.Data.active_run.pending_clear)
            {
                if (!Application.CanStreamedLevelBeLoaded(clearSceneName)) return false;
                SceneManager.LoadScene(clearSceneName); return true;
            }
            bool willResume = save.Data.checkpoint.TryGetValue(sceneName, out var checkpoint) &&
                checkpoint.resumeRequested && !checkpoint.completed && checkpoint.mapIndex > 0;
            if (willResume && !Application.CanStreamedLevelBeLoaded(defaultCheckpointSceneName)) return false;
            FlushTime();
            if (!save.TryUpdateDungeonProgress(data => { DungeonProgressService.Enter(data, sceneName, count); return true; })) return false;
            pendingMapIndex = -1; pendingReturnSceneName = "";
            SceneManager.LoadScene(willResume ? defaultCheckpointSceneName : sceneName);
            return true;
        }

        public bool BeginLoadedDungeon(string sceneName, int count, out int index)
        {
            index = 0;
            var save = StatusSaveFile.Instance;
            if (save == null || count <= 0) return false;
            string key = DungeonProgressService.SceneKey(sceneName);
            bool alreadySelected = save.Data.active_run.sceneName == key &&
                DungeonProgressService.TryGetActiveCheckpoint(save.Data, out var active) && !active.completed;
            int selected = 0;
            bool success = save.TryUpdateDungeonProgress(data =>
            {
                if (alreadySelected)
                {
                    var checkpoint = data.checkpoint[key]; checkpoint.totalMaps = count;
                    checkpoint.mapIndex = Mathf.Clamp(checkpoint.mapIndex, 0, count);
                    selected = checkpoint.resumeRequested ? checkpoint.mapIndex : 0;
                }
                else selected = DungeonProgressService.Enter(data, key, count);
                return true;
            });
            if (!success) return false;
            index = selected;
            if (TryConsumePendingMapIndex(key, out int pending)) index = Mathf.Clamp(pending, 0, count);
            return true;
        }

        public bool SetCheckpoint(string returnSceneName, int nextMapIndex, string checkpointSceneName = "", int totalMaps = 0)
        {
            var save = StatusSaveFile.Instance;
            string destination = string.IsNullOrWhiteSpace(checkpointSceneName) ? defaultCheckpointSceneName : checkpointSceneName;
            if (save == null || !Application.CanStreamedLevelBeLoaded(destination)) return false;
            if (totalMaps <= 0) totalMaps = DungeonMapCatalog.Load(returnSceneName).Length;
            FlushTime();
            bool success = save.TryUpdateDungeonProgress(data =>
            {
                DungeonProgressService.SetCheckpoint(data, returnSceneName, nextMapIndex, totalMaps); return true;
            });
            if (success)
            {
                save.PauseDungeonTimeForSceneChange();
                defaultCheckpointSceneName = destination; pendingMapIndex = -1; pendingReturnSceneName = "";
            }
            return success;
        }

        public bool LoadCheckpointSceneIfAvailable()
        {
            var save = StatusSaveFile.Instance;
            if (save == null) return false;
            if (HasCheckpoint && !Application.CanStreamedLevelBeLoaded(defaultCheckpointSceneName)) return false;
            FlushTime();
            bool resume = false;
            if (!save.TryUpdateDungeonProgress(data => { resume = DungeonProgressService.RestoreCheckpoint(data); return true; })) return false;
            if (!resume) return false;
            save.PauseDungeonTimeForSceneChange();
            SceneManager.LoadScene(defaultCheckpointSceneName); return true;
        }

        public bool ExitCheckpointToSavedNextMap()
        {
            var save = StatusSaveFile.Instance;
            if (!HasCheckpoint || save == null) return false;
            string scene = save.Data.active_run.sceneName;
            if (!Application.CanStreamedLevelBeLoaded(scene)) return false;
            pendingReturnSceneName = scene;
            pendingMapIndex = save.Data.checkpoint[scene].mapIndex;
            SceneManager.LoadScene(scene); return true;
        }
        public bool TryConsumePendingMapIndex(string sceneName, out int mapIndex)
        {
            mapIndex = -1;
            if (pendingMapIndex < 0 || pendingReturnSceneName != DungeonProgressService.SceneKey(sceneName)) return false;
            mapIndex = pendingMapIndex; pendingMapIndex = -1; pendingReturnSceneName = ""; return true;
        }

        public bool CompleteDungeon(string sceneName, int count, string destination)
        {
            var save = StatusSaveFile.Instance;
            if (save == null || !Application.CanStreamedLevelBeLoaded(destination)) return false;
            FlushTime();
            if (!save.TryUpdateDungeonProgress(data => DungeonProgressService.PrepareClear(data, sceneName, count))) return false;
            clearSceneName = destination;
            SceneManager.LoadScene(destination); return true;
        }

        // 기존 외부 호출 호환. 던전별 기록/다른 던전의 체크포인트는 지우지 않습니다.
        public void ClearCheckpoint()
        {
            StatusSaveFile.Instance?.TryUpdateDungeonProgress(data =>
            {
                if (DungeonProgressService.TryGetActiveCheckpoint(data, out var checkpoint) && !checkpoint.completed)
                { checkpoint.mapIndex = 0; checkpoint.resumeRequested = false; checkpoint.broken_traps_snapshot = 0; }
                return true;
            });
        }
        public void RestartDungeonFromFirstMap(string sceneName) => EnterDungeon(sceneName);
    }
}
