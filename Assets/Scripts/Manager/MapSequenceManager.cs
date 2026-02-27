using System;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dungeon.Manager;

public class MapSequenceManager : MonoBehaviour
{
    public static MapSequenceManager Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private JsonBlockBuilder3D builder;

    [Header("Fade")]
    [SerializeField] private ScreenFader fader;
    [SerializeField] private float fadeInSeconds = 0.5f;

    [Header("Folder rule")]
    [SerializeField] private string rootFolder = "Maps";

    [Header("End")]
    [SerializeField] private string endSceneName = "ClearScene";

    private TextAsset[] mapFiles;
    private int currentIndex = 0;

    private PlayerController pendingPlayer;
    private bool isTransitioning;

    private void Awake()
    {
        // ✅ 싱글톤 중복 방지 (맵/씬에 매니저가 2개 있으면 꼬임 방지)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (builder == null)
            builder = FindFirstObjectByType<JsonBlockBuilder3D>();

        // ✅ 비활성 오브젝트까지 포함해서 찾기 (중요!!)
        if (fader == null)
            fader = FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);
    }

    private void Start()
    {
        LoadMapList();
        LoadCurrent();

        // 시작 시 혹시 어두우면 밝히기(비활성일 수 있으니 include로 찾기)
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
            Debug.LogError($"[MapSequence] No json TextAssets in Resources/{folderPath}");
            return;
        }

        mapFiles = mapFiles.OrderBy(t => t.name).ToArray();
        Debug.Log($"[MapSequence] Found maps: {string.Join(", ", mapFiles.Select(m => m.name))}");
    }

    private void LoadCurrent()
    {
        if (builder == null || mapFiles == null || mapFiles.Length == 0) return;

        string sceneName = SceneManager.GetActiveScene().name;
        string path = $"{rootFolder}/{sceneName}/{mapFiles[currentIndex].name}";

        builder.BuildFromJsonPath(path);
        Debug.Log($"[MapSequence] Loaded map {currentIndex + 1}/{mapFiles.Length} : {path}");
    }

    public bool HasNextMap() => mapFiles != null && currentIndex + 1 < mapFiles.Length;

    // ✅ Portal에서 호출 (FadeOut은 Portal이 하고, 여기서는 맵교체 + FadeIn + 조작해제)
    public void LoadNextMapOrFinish(PlayerController pc)
    {
        if (isTransitioning) return; // 중복 호출 방지
        isTransitioning = true;

        pendingPlayer = pc;
        StartCoroutine(CoLoadNextMapOrFinish());
    }

    private IEnumerator CoLoadNextMapOrFinish()
    {
        // 트리거/물리 콜백 탈출
        yield return null;

        if (mapFiles == null || mapFiles.Length == 0)
        {
            Debug.LogError("[MapSequence] mapFiles is empty. Check Resources path.");
            // 혹시라도 잠금이 풀리지 않는 사고 방지
            if (pendingPlayer != null)
                pendingPlayer.SetControl(true, stopImmediately: true);

            pendingPlayer = null;
            isTransitioning = false;
            yield break;
        }

        // ✅ fader가 null이면 다시 찾기 (FadeScreen이 꺼져있을 수 있음)
        if (fader == null)
            fader = FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);

        if (HasNextMap())
        {
            currentIndex++;
            LoadCurrent();

            // (선택) 한 프레임 쉬면 맵 생성 직후 UI/오브젝트 안정화에 도움 됨
            yield return null;

            // ✅ 맵 생성 이후 FadeIn (여기가 “맵 전환 페이드인”의 핵심)
            if (fader != null)
                yield return fader.FadeIn(fadeInSeconds);

            // ✅ FadeIn 끝났으면 조작 해제
            if (pendingPlayer != null)
                pendingPlayer.SetControl(true, stopImmediately: true);

            pendingPlayer = null;
            isTransitioning = false;
        }
        else
        {
            Debug.Log("[MapSequence] Last map cleared!");

            // 마지막엔 ClearScene으로 넘어가게 FadeOut 한 번 더
            if (fader != null)
                yield return fader.FadeOut(1.0f);

            SceneManager.LoadScene(endSceneName);
        }
    }
}
