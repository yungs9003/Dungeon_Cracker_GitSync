using System;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dungeon.Player;
using Dungeon.Map;
using Dungeon.UI;

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

        private PlayerController2D pendingPlayer;
        private bool isTransitioning;

        private void Awake()
        {
            // 싱글톤 중복 방지
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

        private void LoadCurrent()
        {
            if (builder == null || mapFiles == null || mapFiles.Length == 0) return;

            string sceneName = SceneManager.GetActiveScene().name;
            string path = $"{rootFolder}/{sceneName}/{mapFiles[currentIndex].name}";

            builder.BuildFromJsonPath(path);
            Debug.Log($"[MapSequence2D] Loaded map {currentIndex + 1}/{mapFiles.Length} : {path}");
        }

        public bool HasNextMap()
        {
            return mapFiles != null && currentIndex + 1 < mapFiles.Length;
        }

        // 포털 등에서 호출
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
            // 트리거/물리 콜백 탈출
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
                currentIndex++;

                // 필요하면 먼저 FadeOut
                if (fader != null)
                    yield return fader.FadeOut(fadeInSeconds);

                LoadCurrent();

                // 맵 생성 직후 한 프레임 대기
                yield return null;

                // 새 맵 로드 후 FadeIn
                if (fader != null)
                    yield return fader.FadeIn(fadeInSeconds);

                if (pendingPlayer != null)
                    pendingPlayer.SetControl(true, stopImmediately: true);

                pendingPlayer = null;
                isTransitioning = false;
            }
            else
            {
                Debug.Log("[MapSequence2D] Last map cleared!");

                if (fader != null)
                    yield return fader.FadeOut(1.0f);

                SceneManager.LoadScene(endSceneName);
            }
        }

        // 외부에서 현재 맵을 처음부터 다시 불러오고 싶을 때
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