using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapSequenceManager : MonoBehaviour
{
    public static MapSequenceManager Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private JsonBlockBuilder3D builder;

    [Header("Folder rule")]
    [SerializeField] private string rootFolder = "Maps"; // Resources/Maps/...

    [Header("End")]
    [SerializeField] private string endSceneName = ""; // 마지막 맵 끝나면 이 씬으로(원하면)

    private TextAsset[] mapFiles;
    private int currentIndex = 0;

    private void Awake()
    {
        Instance = this;
        if (builder == null) builder = FindFirstObjectByType<JsonBlockBuilder3D>();
    }

    private void Start()
    {
        LoadMapList();
        LoadCurrent();
    }

    private void LoadMapList()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        string folderPath = $"{rootFolder}/{sceneName}";  // 예: "Maps/WorldMap"

        mapFiles = Resources.LoadAll<TextAsset>(folderPath);

        if (mapFiles == null || mapFiles.Length == 0)
        {
            Debug.LogError($"[MapSequence] No json TextAssets in Resources/{folderPath}");
            return;
        }

        // 이름순 정렬 (map_001, map_002 ...)
        mapFiles = mapFiles.OrderBy(t => t.name).ToArray();

        Debug.Log($"[MapSequence] Found maps: {string.Join(", ", mapFiles.Select(m => m.name))}");
    }

    private void LoadCurrent()
    {
        if (builder == null || mapFiles == null || mapFiles.Length == 0) return;

        string sceneName = SceneManager.GetActiveScene().name;
        string path = $"{rootFolder}/{sceneName}/{mapFiles[currentIndex].name}";
        // 예: "Maps/WorldMap/map_001"

        builder.BuildFromJsonPath(path);
        Debug.Log($"[MapSequence] Loaded map {currentIndex + 1}/{mapFiles.Length} : {path}");
    }

    public bool HasNextMap()
    {
        return mapFiles != null && currentIndex + 1 < mapFiles.Length;
    }

    public void LoadNextMapOrFinish()
    {
        if (mapFiles == null || mapFiles.Length == 0) return;

        if (HasNextMap())
        {
            currentIndex++;
            LoadCurrent();
        }
        else
        {
            Debug.Log("[MapSequence] Last map cleared!");

            if (!string.IsNullOrEmpty(endSceneName))
                SceneManager.LoadScene(endSceneName);
            // endSceneName 비우면 그냥 현재 씬 유지(원하면 Clear UI 띄우는 로직 넣으면 됨)
        }
    }
}
