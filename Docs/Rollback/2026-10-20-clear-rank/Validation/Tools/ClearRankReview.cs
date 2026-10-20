using System.IO;
using Dungeon.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ClearRankReview
{
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/ClearScene.unity");
        new GameObject("ClearRankChecks").AddComponent<ClearRankChecks>();
        EditorSceneManager.SaveScene(scene);
        var data = StatusSaveDefaults.Create();
        DungeonProgressService.Enter(data, "DUNGEON_001", 5);
        data.clear_time_seconds = 127; data.hit_count = 24; data.broken_traps = 23;
        DungeonProgressService.PrepareClear(data, "DUNGEON_001", 5);
        string json = StatusSaveDefaults.Serialize(data).Replace("\"save_version\": 5", "\"save_version\": 4")
            .Replace("\"clear_time_seconds\": 127", "\"clear_time_seconds\": 127.54")
            .Replace("\"run_time_seconds\": 127", "\"run_time_seconds\": 127.54");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "savefile.test.json"), json);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/ClearScene.unity", true) };
        EditorApplication.isPlaying = true;
    }
    public static void Finish(int code) => EditorApplication.Exit(code);
}
