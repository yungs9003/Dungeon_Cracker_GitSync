using System;
using System.Linq;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
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

        private TextAsset[] mapFiles;
        private int currentIndex = 0;

        // PlayerController, PlayerController2D 둘 다 받을 수 있게 Component로 보관한다.
        private Component pendingPlayer;
        private bool isTransitioning;

        public int CurrentIndex => currentIndex;
        public int MapCount => mapFiles == null ? 0 : mapFiles.Length;
        public string EndSceneName => endSceneName;

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
            LoadMapList();

            if (MapCount == 0) return;
            if (!SaveManager.GetOrCreate().BeginLoadedDungeon(SceneManager.GetActiveScene().name, MapCount, out currentIndex))
            { Debug.LogError("[MapSequence] 던전 기록을 준비/저장하지 못했습니다."); return; }

            if (mapFiles != null && mapFiles.Length > 0 && currentIndex >= mapFiles.Length)
            {
                Debug.Log("[MapSequence] Pending map index is past the last map. Go to ClearScene.");
                FinishDungeon();
                return;
            }

            LoadCurrent();

            if (fader == null)
                fader = FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);

            if (fader != null)
                StartCoroutine(fader.FadeIn(0.01f));
        }

        private void LoadMapList()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            string folderPath = DungeonMapCatalog.ResourcePath(sceneName, rootFolder);

            mapFiles = DungeonMapCatalog.Load(sceneName, rootFolder);

            if (mapFiles == null || mapFiles.Length == 0)
            {
                Debug.LogError($"[MapSequence] No json TextAssets in Resources/{folderPath}");
                return;
            }

            Debug.Log($"[MapSequence] Found maps: {string.Join(", ", mapFiles.Select(m => m.name))}");
        }

        private void LoadCurrent()
        {
            if (builder == null || mapFiles == null || mapFiles.Length == 0)
                return;

            currentIndex = Mathf.Clamp(currentIndex, 0, mapFiles.Length - 1);

            string sceneName = SceneManager.GetActiveScene().name;
            string path = $"{DungeonMapCatalog.ResourcePath(sceneName, rootFolder)}/{mapFiles[currentIndex].name}";

            builder.BuildFromJsonPath(path);
            Debug.Log($"[MapSequence] Loaded map {currentIndex + 1}/{mapFiles.Length} : {path}");
        }

        public bool HasNextMap()
        {
            return mapFiles != null && currentIndex + 1 < mapFiles.Length;
        }

        // 체크포인트 도달 시 저장해야 하는 "다음 맵 index".
        // 마지막 맵에서 체크포인트를 찍은 경우 mapFiles.Length가 반환될 수 있고,
        // 이 값으로 돌아오면 Start()에서 ClearScene으로 보낸다.
        public int GetNextMapIndexForCheckpoint()
        {
            return currentIndex + 1;
        }

        // 기존 3D 포탈/2D 포탈 모두 이 함수로 호출 가능하다.
        public void LoadNextMapOrFinish(Component playerController)
        {
            if (isTransitioning) return;
            isTransitioning = true;

            pendingPlayer = playerController;
            StartCoroutine(CoLoadNextMapOrFinish());
        }

        private IEnumerator CoLoadNextMapOrFinish()
        {
            yield return null;

            if (mapFiles == null || mapFiles.Length == 0)
            {
                Debug.LogError("[MapSequence] mapFiles is empty. Check Resources path.");

                SetPlayerControl(pendingPlayer, true, true);

                pendingPlayer = null;
                isTransitioning = false;
                yield break;
            }

            if (fader == null)
                fader = FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);

            if (HasNextMap())
            {
                currentIndex++;
                LoadCurrent();

                yield return null;

                if (fader != null)
                    yield return fader.FadeIn(fadeInSeconds);

                SetPlayerControl(pendingPlayer, true, true);

                pendingPlayer = null;
                isTransitioning = false;
            }
            else
            {
                Debug.Log("[MapSequence] Last map cleared!");

                if (fader != null)
                    yield return fader.FadeOut(1.0f);

                if (!FinishDungeon())
                {
                    SetPlayerControl(pendingPlayer, true, true);
                    if (fader != null) yield return fader.FadeIn(fadeInSeconds);
                    isTransitioning = false;
                }
            }
        }

        private bool FinishDungeon() => SaveManager.GetOrCreate().CompleteDungeon(
            SceneManager.GetActiveScene().name, MapCount, endSceneName);

        private void SetPlayerControl(Component playerController, bool enabled, bool stopImmediately)
        {
            if (playerController == null) return;

            MethodInfo method = playerController.GetType().GetMethod(
                "SetControl",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(bool), typeof(bool) },
                null
            );

            if (method != null)
            {
                method.Invoke(playerController, new object[] { enabled, stopImmediately });
                return;
            }

            Debug.LogWarning($"[MapSequence] {playerController.GetType().Name}에 SetControl(bool, bool) 메서드가 없습니다.");
        }
    }
}
