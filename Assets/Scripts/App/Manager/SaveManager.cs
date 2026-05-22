using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dungeon.Core;

namespace Dungeon.Manager
{
    /// <summary>
    /// SaveManager는 저장 데이터와 체크포인트 정보를 관리한다.
    ///
    /// 주의:
    /// - 이 스크립트는 Assets/Scripts/App 쪽에 둔다.
    /// - GamePlay asmdef에서 Dungeon.App을 참조하지 않아도 되도록,
    ///   Respawn2D 쪽에서는 이 타입을 직접 참조하지 않고 reflection으로 호출하는 구조를 사용한다.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        [Header("Scene Names")]
        [SerializeField] private string defaultCheckpointSceneName = "CheckpointScene";
        [SerializeField] private string clearSceneName = "ClearScene";

        private PlayerSaveData data = new();

        private string SavePath => Path.Combine(Application.persistentDataPath, "player_save.json");

        // --------------------------------------------------------------------
        // Reflection 호환용 공개 프로퍼티
        // Respawn2D_reflection_no_asmdef_cycle.cs에서 문자열 이름으로 찾을 수 있게 유지한다.
        // --------------------------------------------------------------------

        public bool HasCheckpoint => data != null && data.hasCheckpoint;

        // 기존 코드와 호환용 별칭
        public bool HasActiveCheckpoint => HasCheckpoint;

        public string CheckpointSceneName
        {
            get
            {
                if (data == null)
                    data = new PlayerSaveData();

                if (string.IsNullOrEmpty(data.checkpointSceneName))
                    data.checkpointSceneName = defaultCheckpointSceneName;

                return data.checkpointSceneName;
            }
        }

        public int PendingMapIndex => data != null ? data.pendingMapIndex : -1;
        public string PendingReturnSceneName => data != null ? data.pendingReturnSceneName : "";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Load();

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // ClearScene에 들어가면 마지막 체크포인트 정보를 초기화한다.
            if (scene.name == clearSceneName)
                ClearCheckpoint();
        }

        // --------------------------------------------------------------------
        // Basic save / load
        // --------------------------------------------------------------------

        public void Load()
        {
            try
            {
                if (!File.Exists(SavePath))
                {
                    data = CreateDefaultData();
                    Save();
                    return;
                }

                string json = File.ReadAllText(SavePath);
                data = JsonUtility.FromJson<PlayerSaveData>(json);

                if (data == null)
                    data = CreateDefaultData();

                ValidateData();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Load failed: {e}");
                data = CreateDefaultData();
            }
        }

        public void Save()
        {
            try
            {
                ValidateData();

                string directory = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(SavePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Save failed: {e}");
            }
        }

        private PlayerSaveData CreateDefaultData()
        {
            PlayerSaveData created = new PlayerSaveData();

            if (created.dungeons == null)
                created.dungeons = new();

            created.hasCheckpoint = false;
            created.checkpointSceneName = defaultCheckpointSceneName;
            created.checkpointReturnSceneName = "";
            created.checkpointNextMapIndex = 0;
            created.pendingReturnSceneName = "";
            created.pendingMapIndex = -1;

            return created;
        }

        private void ValidateData()
        {
            if (data == null)
                data = CreateDefaultData();

            if (data.dungeons == null)
                data.dungeons = new();

            if (string.IsNullOrEmpty(data.checkpointSceneName))
                data.checkpointSceneName = defaultCheckpointSceneName;

            // pendingMapIndex는 예약이 없을 때 -1이어야 한다.
            if (string.IsNullOrEmpty(data.pendingReturnSceneName) && data.pendingMapIndex < 0)
                data.pendingMapIndex = -1;
        }

        // --------------------------------------------------------------------
        // Dungeon progress
        // --------------------------------------------------------------------

        public DungeonProgress GetOrCreateDungeonProgress(string dungeonId)
        {
            ValidateData();

            for (int i = 0; i < data.dungeons.Count; i++)
            {
                if (data.dungeons[i].dungeonId == dungeonId)
                {
                    EnsureCriteriaLength(data.dungeons[i]);
                    return data.dungeons[i];
                }
            }

            DungeonProgress created = new DungeonProgress
            {
                dungeonId = dungeonId,
                criteria = new bool[4],
                bestTimeMs = 0,
                clearCount = 0,
                lastPlayedUtc = ""
            };

            data.dungeons.Add(created);
            return created;
        }

        private void EnsureCriteriaLength(DungeonProgress progress)
        {
            if (progress.criteria == null || progress.criteria.Length != 4)
                progress.criteria = new bool[4];
        }

        // --------------------------------------------------------------------
        // Checkpoint
        // --------------------------------------------------------------------

        /// <summary>
        /// 체크포인트에 도달했을 때 호출한다.
        /// returnSceneName으로 돌아갔을 때 nextMapIndex 맵부터 재개하도록 저장한다.
        /// </summary>
        public void SetCheckpoint(string returnSceneName, int nextMapIndex, string checkpointSceneName = "")
        {
            ValidateData();

            if (string.IsNullOrEmpty(returnSceneName))
            {
                Debug.LogWarning("[SaveManager] SetCheckpoint failed: returnSceneName is empty.");
                return;
            }

            data.hasCheckpoint = true;
            data.checkpointSceneName = string.IsNullOrEmpty(checkpointSceneName)
                ? defaultCheckpointSceneName
                : checkpointSceneName;

            data.checkpointReturnSceneName = returnSceneName;
            data.checkpointNextMapIndex = Mathf.Max(0, nextMapIndex);

            // 이전에 남아 있을 수 있는 1회성 예약값은 제거한다.
            data.pendingReturnSceneName = "";
            data.pendingMapIndex = -1;

            Save();

            Debug.Log(
                $"[SaveManager] Checkpoint saved. " +
                $"CheckpointScene={data.checkpointSceneName}, " +
                $"ReturnScene={data.checkpointReturnSceneName}, " +
                $"NextMapIndex={data.checkpointNextMapIndex}"
            );
        }

        /// <summary>
        /// HP 0 리스폰용.
        /// 활성 체크포인트가 있으면 CheckpointScene으로 이동한다.
        /// Respawn2D reflection 코드에서 이 메서드명을 찾으므로 이름을 바꾸지 않는 것이 좋다.
        /// </summary>
        public bool LoadCheckpointSceneIfAvailable()
        {
            ValidateData();

            if (!HasCheckpoint)
                return false;

            string sceneName = CheckpointSceneName;
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("[SaveManager] Checkpoint scene name is empty.");
                return false;
            }

            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            return true;
        }

        /// <summary>
        /// 체크포인트 씬의 출구에서 호출한다.
        /// 저장된 던전 씬으로 돌아가고, 그 씬의 MapSequenceManager가 pendingMapIndex부터 로드한다.
        /// </summary>
        public bool ExitCheckpointToSavedNextMap()
        {
            ValidateData();

            if (!HasCheckpoint)
            {
                Debug.LogWarning("[SaveManager] No active checkpoint. Cannot exit checkpoint.");
                return false;
            }

            if (string.IsNullOrEmpty(data.checkpointReturnSceneName))
            {
                Debug.LogWarning("[SaveManager] checkpointReturnSceneName is empty.");
                return false;
            }

            data.pendingReturnSceneName = data.checkpointReturnSceneName;
            data.pendingMapIndex = Mathf.Max(0, data.checkpointNextMapIndex);

            Save();

            Debug.Log(
                $"[SaveManager] Exit checkpoint. " +
                $"ReturnScene={data.pendingReturnSceneName}, PendingMapIndex={data.pendingMapIndex}"
            );

            SceneManager.LoadScene(data.pendingReturnSceneName, LoadSceneMode.Single);
            return true;
        }

        /// <summary>
        /// MapSequenceManager.Start()에서 호출한다.
        /// 체크포인트 출구가 예약한 맵 index가 현재 씬에 해당하면 한 번만 꺼내간다.
        /// </summary>
        public bool TryConsumePendingMapIndex(string sceneName, out int mapIndex)
        {
            ValidateData();

            mapIndex = -1;

            if (data.pendingMapIndex < 0)
                return false;

            if (data.pendingReturnSceneName != sceneName)
                return false;

            mapIndex = data.pendingMapIndex;

            data.pendingReturnSceneName = "";
            data.pendingMapIndex = -1;

            Save();

            Debug.Log($"[SaveManager] Consumed pending map index: {mapIndex} for scene {sceneName}");
            return true;
        }

        /// <summary>
        /// 체크포인트 정보와 체크포인트 출구 예약 정보를 모두 초기화한다.
        /// ClearScene 진입 시와, 체크포인트가 없는 상태에서 던전 전체 재시작 시 사용한다.
        /// </summary>
        public void ClearCheckpoint()
        {
            ValidateData();

            data.hasCheckpoint = false;
            data.checkpointReturnSceneName = "";
            data.checkpointNextMapIndex = 0;

            data.pendingReturnSceneName = "";
            data.pendingMapIndex = -1;

            if (string.IsNullOrEmpty(data.checkpointSceneName))
                data.checkpointSceneName = defaultCheckpointSceneName;

            Save();

            Debug.Log("[SaveManager] Checkpoint cleared.");
        }
    }
}
