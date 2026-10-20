using System.IO;
using Dungeon.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MapDirectionRuntimeReview
{
    public static void Run()
    {
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../.."));
        string audit=File.ReadAllText(Path.Combine(root,"Docs/Rollback/2026-10-20-map-directions/direction-audit.json"));
        File.WriteAllText("Assets/Resources/CodexMapDirectionAudit.json","{\"entries\":"+audit+"}");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath,"savefile.test.json"),StatusSaveDefaults.Serialize(StatusSaveDefaults.Create()));
        AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("MapDirectionChecks").AddComponent<MapDirectionRuntimeChecks>();
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/CodexMapDirectionChecks.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/CodexMapDirectionChecks.unity",true)};
        EditorApplication.isPlaying=true;
    }
}
