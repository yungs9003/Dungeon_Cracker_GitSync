using System.IO;
using System.Reflection;
using Dungeon.Manager;
using Dungeon.Core;
using Dungeon.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayTimeRuntimeReview
{
    public static void Run()
    {
        const string folder = "Assets/TimeValidationScenes";
        Directory.CreateDirectory(folder);
        string[] names = { "MainMenu", "WorldMap", "DUNGEON_001", "DUNGEON_002", "CheckpointScene", "ClearScene" };
        var scenes = new EditorBuildSettingsScene[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (names[i] == "MainMenu") new GameObject("RuntimeChecks").AddComponent<PlayTimeRuntimeChecks>();
            if (names[i].StartsWith("DUNGEON_"))
            {
                new GameObject("MapSequence").AddComponent<MapSequenceManager2D>();
                var go = new GameObject("GameManager"); go.AddComponent<BoxCollider2D>(); go.AddComponent<DungeonRunTracker2D>();
            }
            if (names[i] == "ClearScene")
            {
                var clear = new GameObject("ClearResult").AddComponent<ClearResultUI>();
                var label = new GameObject("ClearTime", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                typeof(ClearResultUI).GetField("clearTimeText", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(clear, label);
            }
            string path = folder + "/" + names[i] + ".unity";
            EditorSceneManager.SaveScene(scene, path); scenes[i] = new EditorBuildSettingsScene(path, true);
        }
        EditorBuildSettings.scenes = scenes;
        // Isolated project only: start fresh and exercise the real bootstrap/factory.
        string save = Path.Combine(Application.streamingAssetsPath, "savefile.test.json");
        if (File.Exists(save)) File.Delete(save);
        string backup = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/DungeonSaveBackups"));
        foreach (string file in Directory.Exists(backup) ? Directory.GetFiles(backup, "*.json") : new string[0]) File.Delete(file);
        EditorSceneManager.OpenScene(scenes[0].path);
        EditorApplication.isPlaying = true;
    }
    public static void Finish(int code) => EditorApplication.Exit(code);

    public static void RunDirect()
    {
        EditorSceneManager.OpenScene("Assets/TimeValidationScenes/DUNGEON_001.unity");
        new GameObject("SceneSaveFile").AddComponent<StatusSaveFile>();
        new GameObject("DirectChecks").AddComponent<PlayTimeDirectChecks>();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        string active = Path.Combine(Application.streamingAssetsPath, "savefile.test.json");
        if (File.Exists(active)) File.Delete(active);
        EditorApplication.isPlaying = true;
    }
}
