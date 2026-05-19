using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dungeon.Player;
using Dungeon.Map;
using Dungeon.UI;
using Dungeon.Core;

namespace Dungeon.Manager
{
    public class MapSequenceManager2D : MonoBehaviour
    {
        public static MapSequenceManager2D Instance { get; private set; }

        [Header("Refs")]
        [SerializeField] private JsonBlockBuilder2D builder;

        [Header("Fade")]
        [SerializeField] private ScreenFader fader;
        [SerializeField] private float fadeInSeconds = 0.5f;

        [Header("Folder rule")]
        [SerializeField] private string rootFolder = "Maps";

        [Header("End")]
        [SerializeField] private string endSceneName = "ClearScene";

        [Header("Checkpoint")]
        [SerializeField] private string checkpointSceneName = "CheckpointScene";

        private TextAsset[] mapFiles;
        private int currentIndex = 0;

        private PlayerController2D pendingPlayer;
        private bool isTransitioning;

        public int CurrentIndex => currentIndex;
        public int MapCount => mapFiles != null ? mapFiles.Length : 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (builder == null)
                builder = FindFirstObjectByType<JsonBlockBuilder2D>();

            if (fader == null)
                fader = FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);
        }

        private void Start()
        {
            SaveFileManager.EnsureSaveFileExists();

            LoadMapList();

            TryApplyCheckpointResume();

            LoadCurrent();

            if (fader == null)
                fader = FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);

            if (fader != null)
                StartCoroutine(fader.FadeIn(0.01f));
        }

        private void LoadMapList()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            string folderPath = $"{rootFolder}/{sceneName}";

            mapFiles = Resources.LoadAll<TextAsset>(folderPath);

            if (mapFiles == null || mapFiles.Length == 0)
            {
                Debug.LogError($"[MapSequence2D] No json TextAssets in Resources/{folderPath}");
                return;
            }

            mapFiles = mapFiles.OrderBy(t => t.name).ToArray();

            Debug.Log($"[MapSequence2D] Found maps: {string.Join(", ", mapFiles.Select(m => m.name))}");
        }

        private void TryApplyCheckpointResume()
        {
            if (!SaveFileManager.HasCheckpointResumeRequest())
                return;

            string savedSceneName = SaveFileManager.GetCheckpointSceneName();
            string currentSceneName = SceneManager.GetActiveScene().name;

            if (savedSceneName != currentSceneName)
            {
                Debug.LogWarning(
                    $"[MapSequence2D] Checkpoint scene mismatch. Saved={savedSceneName}, Current={currentSceneName}"
                );
                return;
            }

            int savedMapIndex = SaveFileManager.GetCheckpointMapIndex();
            int nextIndex = savedMapIndex + 1;

            if (mapFiles == null || mapFiles.Length == 0)
                return;

            if (nextIndex >= mapFiles.Length)
            {
                SaveFileManager.ClearCheckpointResumeRequest();

                Debug.Log("[MapSequence2D] Checkpoint resume target is after last map. Loading clear scene.");
                SceneManager.LoadScene(endSceneName);
                return;
            }

            currentIndex = nextIndex;
            SaveFileManager.ClearCheckpointResumeRequest();

            Debug.Log($"[MapSequence2D] Resume from checkpoint. Start index={currentIndex}");
        }

        private void LoadCurrent()
        {
            if (builder == null || mapFiles == null || mapFiles.Length == 0)
                return;

            if (currentIndex < 0)
                currentIndex = 0;

            if (currentIndex >= mapFiles.Length)
            {
                Debug.LogWarning("[MapSequence2D] currentIndex is out of range. Loading end scene.");
                SceneManager.LoadScene(endSceneName);
                return;
            }

            string sceneName = SceneManager.GetActiveScene().name;
            string path = $"{rootFolder}/{sceneName}/{mapFiles[currentIndex].name}";

            builder.BuildFromJsonPath(path);

            Debug.Log($"[MapSequence2D] Loaded map {currentIndex + 1}/{mapFiles.Length} : {path}");
        }

        public bool HasNextMap()
        {
            return mapFiles != null && currentIndex + 1 < mapFiles.Length;
        }

        public void LoadNextMapOrFinish(PlayerController2D pc)
        {
            if (isTransitioning) return;

            isTransitioning = true;
            pendingPlayer = pc;

            if (pendingPlayer != null)
                pendingPlayer.SetControl(false, stopImmediately: true);

            StartCoroutine(CoLoadNextMapOrFinish());
        }

        private IEnumerator CoLoadNextMapOrFinish()
        {
            yield return null;

            if (mapFiles == null || mapFiles.Length == 0)
            {
                Debug.LogError("[MapSequence2D] mapFiles is empty. Check Resources path.");

                if (pendingPlayer != null)
                    pendingPlayer.SetControl(true, stopImmediately: true);

                pendingPlayer = null;
                isTransitioning = false;
                yield break;
            }

            if (fader == null)
                fader = FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);

            if (HasNextMap())
            {
                SaveFileManager.AddGold(500);
                currentIndex++;

                if (fader != null)
                    yield return fader.FadeOut(fadeInSeconds);

                LoadCurrent();

                yield return null;

                if (fader != null)
                    yield return fader.FadeIn(fadeInSeconds);

                if (pendingPlayer != null)
                    pendingPlayer.SetControl(true, stopImmediately: true);

                pendingPlayer = null;
                isTransitioning = false;
            }
            else
            {
                SaveFileManager.AddGold(500);
                Debug.Log("[MapSequence2D] Last map cleared!");

                if (fader != null)
                    yield return fader.FadeOut(1.0f);

                SceneManager.LoadScene(endSceneName);
            }
        }

        public void EnterCheckpoint(PlayerController2D pc)
        {
            if (isTransitioning) return;

            isTransitioning = true;
            pendingPlayer = pc;

            if (pendingPlayer != null)
                pendingPlayer.SetControl(false, stopImmediately: true);

            StartCoroutine(CoEnterCheckpoint());
        }

        private IEnumerator CoEnterCheckpoint()
        {
            yield return null;

            string currentSceneName = SceneManager.GetActiveScene().name;
            SaveFileManager.AddGold(500);
            SaveFileManager.SaveCheckpoint(currentSceneName, currentIndex);

            if (fader == null)
                fader = FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);

            if (fader != null)
                yield return fader.FadeOut(fadeInSeconds);

            SceneManager.LoadScene(checkpointSceneName);
        }

        public void ReloadCurrentMap(PlayerController2D pc = null)
        {
            if (isTransitioning) return;

            isTransitioning = true;
            pendingPlayer = pc;

            if (pendingPlayer != null)
                pendingPlayer.SetControl(false, stopImmediately: true);

            StartCoroutine(CoReloadCurrentMap());
        }

        private IEnumerator CoReloadCurrentMap()
        {
            if (fader == null)
                fader = FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);

            if (fader != null)
                yield return fader.FadeOut(fadeInSeconds);

            LoadCurrent();

            yield return null;

            if (fader != null)
                yield return fader.FadeIn(fadeInSeconds);

            if (pendingPlayer != null)
                pendingPlayer.SetControl(true, stopImmediately: true);

            pendingPlayer = null;
            isTransitioning = false;
        }
    }
}