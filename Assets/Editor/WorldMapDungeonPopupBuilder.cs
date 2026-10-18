using System;
using System.Linq;
using Dungeon.UI;
using Dungeon.UI.Tutorial;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class WorldMapDungeonPopupBuilder
{
    public const string PrefabPath = "Assets/Prefabs/UI/WorldMap/DungeonInfoPopup.prefab";
    private const string Art = "Assets/Sprites/UI/WorldMap/DungeonPopup/";
    [MenuItem("Tools/Dungeon/Configure WorldMap Dungeon Popup")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try { Build(); }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }
    public static void Build()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var prefab = BuildPrefab();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/WorldMap.unity", OpenSceneMode.Single);
        var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DungeonPopupPanelController>(true)).Single();
        var root = controller.gameObject;
        // 하단 바 위에서도 외부 클릭을 먼저 받고, 전체 화면 도움말은 이 Canvas 위에 둡니다.
        var modalCanvas = root.GetComponent<Canvas>();
        if (modalCanvas == null) modalCanvas = root.AddComponent<Canvas>();
        modalCanvas.overrideSorting = true; modalCanvas.sortingOrder = 100;
        if (root.GetComponent<GraphicRaycaster>() == null) root.AddComponent<GraphicRaycaster>();
        for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
        var image = root.GetComponent<Image>(); image.enabled = true; image.color = Color.clear; image.sprite = null; image.type = Image.Type.Simple; image.raycastTarget = true;
        var outside = root.GetComponent<Button>() ?? root.AddComponent<Button>(); outside.targetGraphic = image; outside.transition = Selectable.Transition.None; NoNavigation(outside);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform); instance.name = "PopupWindow";
        controller.ConfigureReferenceView(instance.GetComponent<DungeonInfoPopupUI>(), outside);
        // 삭제한 기존 미리보기/사이드 패널에 대한 참조는 남기지 않습니다.
        var serialized = new SerializedObject(controller);
        foreach (string field in new[] { "previewImage", "dungeonNameText", "descriptionText", "farmItemContainer", "farmItemPrefab" }) serialized.FindProperty(field).objectReferenceValue = null;
        serialized.FindProperty("clearCircles").arraySize = 0; serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("[WorldMapDungeonPopupBuilder] SVG reference popup installed. Dungeon record storage remains unchanged.");
    }
    private static GameObject BuildPrefab()
    {
        var root = UI("PopupWindow", null, 0, 0, 776, 484);
        var rect = root.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f; rect.localScale = new Vector3(.8f, .8f, 1);
        var background = root.AddComponent<Image>(); background.sprite = Sprite("PopupFrame.png"); background.raycastTarget = true;
        var number = Text("DungeonNumber", root.transform, "NO.01", 60, 56, 64, 176, 80);
        var name = Text("DungeonName", root.transform, "서늘숲", 60, 248, 64, 380, 80);
        name.enableAutoSizing = true; name.fontSizeMin = 24; name.fontSizeMax = 60; name.color = Color.white; name.enableVertexGradient = true;
        Color purple = new Color32(163, 32, 142, 255), blue = new Color32(101, 63, 255, 255);
        name.colorGradient = new VertexGradient(purple, blue, purple, blue);
        var clears = Text("ClearCount", root.transform, "클리어 횟수 : 0회", 36, 80, 216, 392, 60);
        var progress = Text("CurrentProgress", root.transform, "현재 진행도 : 0/?층", 36, 80, 276, 392, 60);
        clears.enableAutoSizing = progress.enableAutoSizing = true; clears.fontSizeMin = progress.fontSizeMin = 24;
        clears.fontSizeMax = progress.fontSizeMax = 36;
        var enterRoot = UI("EnterButton", root.transform, 500, 308, 220, 124);
        var enterImage = enterRoot.AddComponent<Image>(); enterImage.color = Color.clear;
        var enter = enterRoot.AddComponent<Button>(); enter.targetGraphic = enterImage;
        var colors = enter.colors; colors.normalColor = colors.selectedColor = Color.clear; colors.highlightedColor = new Color(0, 0, 0, .08f); colors.pressedColor = new Color(0, 0, 0, .18f); colors.disabledColor = new Color(.4f, .4f, .4f, .5f); enter.colors = colors; NoNavigation(enter);
        var helpRoot = UI("TutorialHelp_DungeonInfo", root.transform, 640, 44, 84, 80);
        var helpImage = helpRoot.AddComponent<Image>(); helpImage.sprite = Sprite("HelpNormal.png"); helpImage.preserveAspect = true;
        var helpButton = helpRoot.AddComponent<Button>(); helpButton.targetGraphic = helpImage; helpButton.transition = Selectable.Transition.None; NoNavigation(helpButton);
        var help = helpRoot.AddComponent<TutorialLauncherUI>();
        var view = root.AddComponent<DungeonInfoPopupUI>(); view.Configure(number, name, clears, progress, enter, help, Sprite("HelpNormal.png"), Sprite("HelpHover.png"));
        view.ConfigureHelp(AssetDatabase.LoadAssetAtPath<TutorialSlides>("Assets/Resources/UI/Tutorial/DungeonInfoTutorial.asset"),
            AssetDatabase.LoadAssetAtPath<GameObject>(TutorialSceneBuilder.OverlayPath).GetComponent<TutorialOverlayUI>());
        root.AddComponent<DungeonInfoPopupLayout>();
        var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); Object.DestroyImmediate(root); return saved;
    }
    private static GameObject UI(string name, Transform parent, float x, float y, float width, float height)
    {
        var root = new GameObject(name, typeof(RectTransform)); root.layer = 5;
        if (parent != null) root.transform.SetParent(parent, false);
        var rect = root.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return root;
    }
    private static TextMeshProUGUI Text(string name, Transform parent, string value, float size, float x, float y, float width, float height)
    {
        var root = UI(name, parent, x, y, width, height); var text = root.AddComponent<TextMeshProUGUI>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Cafe24Danjunghae-v2.0 SDF.asset"); text.text = value; text.fontSize = size; text.color = Color.black;
        text.alignment = TextAlignmentOptions.MidlineLeft; text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis; text.raycastTarget = false; return text;
    }
    private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name) ?? throw new InvalidOperationException("Missing dungeon popup art: " + name);
    private static void NoNavigation(Selectable control) { var navigation = control.navigation; navigation.mode = Navigation.Mode.None; control.navigation = navigation; }
}
