using System.IO;
using Dungeon.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FieldThemeRuntimeReview
{
    public static void Run()
    {
        Directory.CreateDirectory(Application.streamingAssetsPath);
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "savefile.test.json"),
            StatusSaveDefaults.Serialize(StatusSaveDefaults.Create()));
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("FieldThemeRuntimeChecks").AddComponent<FieldThemeRuntimeChecks>();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/CodexFieldThemeChecks.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/CodexFieldThemeChecks.unity", true) };
        EditorApplication.isPlaying = true;
    }
}
