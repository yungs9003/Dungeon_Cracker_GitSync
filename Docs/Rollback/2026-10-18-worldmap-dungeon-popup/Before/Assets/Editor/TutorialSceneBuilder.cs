using System;
using System.Linq;
using Dungeon.UI;
using Dungeon.UI.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class TutorialSceneBuilder
{
    public const string OverlayPath = "Assets/Prefabs/UI/Tutorial/TutorialOverlay.prefab";
    public const string HelpPath = "Assets/Prefabs/UI/Tutorial/TutorialHelpButton.prefab";
    public const string DataRoot = "Assets/Resources/UI/Tutorial/";
    private const string Art = "Assets/Sprites/UI/Tutorial/";
    private static TutorialOverlayUI overlay;
    private static GameObject help;

    [MenuItem("Tools/Dungeon/Configure Tutorial Windows")]
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
        var world = LoadOrCreateSlides("WorldMapTutorial", 5);
        var dungeon = LoadOrCreateSlides("DungeonInfoTutorial", 4);
        var status = LoadOrCreateSlides("StatusTutorial", 5);
        var module = LoadOrCreateSlides("ModuleTutorial", 3);
        overlay = BuildOverlay(); help = BuildHelp();
        const string shared = "Assets/Prefabs/UI/SharedStatus.prefab";
        var root = PrefabUtility.LoadPrefabContents(shared);
        try
        {
            var controller = root.GetComponentInChildren<StatusWindowController>(true);
            AddHelp(StatusPanel(controller), "TutorialHelp_Status", status);
            PrefabUtility.SaveAsPrefabAsset(root, shared);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/WorldMap.unity", OpenSceneMode.Single);
        var canvas = scene.GetRootGameObjects().First(r => r.name == "Canvas").GetComponent<RectTransform>();
        AddHelp(canvas, "TutorialHelp_WorldMap", world);
        var dungeonPanel = canvas.GetComponentInChildren<DungeonPopupPanelController>(true).transform.Find("PopupWindow").GetComponent<RectTransform>();
        var dungeonHelp = AddHelp(dungeonPanel, "TutorialHelp_DungeonInfo", dungeon);
        // 기존 닫기 버튼과 겹치지 않도록 같은 상단 줄의 왼쪽에 둡니다.
        var close = dungeonPanel.Find("CloseButton") as RectTransform;
        if (close != null)
        {
            var rect = dungeonHelp.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            Vector3 center = dungeonPanel.InverseTransformPoint(close.TransformPoint(close.rect.center));
            rect.anchoredPosition = new Vector2(center.x - close.rect.width * .5f - 48, center.y);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        scene = EditorSceneManager.OpenScene("Assets/Scenes/Module.unity", OpenSceneMode.Single);
        var modulePanel = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RectTransform>(true)).First(t => t.name == "ModulePageRoot");
        AddHelp(modulePanel, "TutorialHelp_Module", module);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        scene = EditorSceneManager.OpenScene("Assets/Scenes/CheckpointScene.unity", OpenSceneMode.Single);
        var localStatus = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StatusWindowController>(true)).Single();
        AddHelp(StatusPanel(localStatus), "TutorialHelp_Status", status);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("[TutorialSceneBuilder] WorldMap 5 / DungeonInfo 4 / Status 5 / Module 3 slides and launchers linked.");
    }
    private static RectTransform StatusPanel(StatusWindowController controller)
    {
        var data = new SerializedObject(controller);
        var root = (GameObject)data.FindProperty("statusWindowRoot").objectReferenceValue;
        var panel = root.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "StatusPanel");
        if (panel.GetComponent<StatusWindowLayout>() == null) panel.gameObject.AddComponent<StatusWindowLayout>();
        return panel;
    }
    private static TutorialLauncherUI AddHelp(RectTransform parent, string name, TutorialSlides slides)
    {
        var existing = parent.Find(name);
        var root = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(help, parent);
        root.name = name;
        var rect = root.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(20, -20); rect.sizeDelta = new Vector2(64, 64);
        var view = root.GetComponent<TutorialLauncherUI>(); view.Configure(slides, overlay, Sprite("button_normal.png"), Sprite("button_on_mouse.png"));
        root.transform.SetAsLastSibling(); EditorUtility.SetDirty(view);
        PrefabUtility.RecordPrefabInstancePropertyModifications(view); PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        return view;
    }
    private static TutorialSlides LoadOrCreateSlides(string name, int count)
    {
        string path = DataRoot + name + ".asset";
        var slides = AssetDatabase.LoadAssetAtPath<TutorialSlides>(path);
        if (slides != null) return slides;
        slides = ScriptableObject.CreateInstance<TutorialSlides>();
        slides.slides = Enumerable.Repeat(Sprite("Slides/info_worldmap_1.png"), count).ToArray();
        AssetDatabase.CreateAsset(slides, path); return slides;
    }
    private static TutorialOverlayUI BuildOverlay()
    {
        var root = UI("TutorialOverlay", null);
        var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = 32760;
        var scaler = root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        root.AddComponent<GraphicRaycaster>();
        var body = UI("Modal", root.transform); Stretch(body.GetComponent<RectTransform>());
        var dim = body.AddComponent<Image>(); dim.color = new Color32(0, 0, 0, 200); dim.raycastTarget = true;
        var backgroundRoot = UI("SlideBackground", body.transform);
        var backgroundRect = backgroundRoot.GetComponent<RectTransform>();
        backgroundRect.sizeDelta = StatusWindowLayout.ReferenceSize; backgroundRect.anchoredPosition = new Vector2(0, 65.5f);
        var backing = backgroundRoot.AddComponent<Image>(); backing.color = Color.white; backing.raycastTarget = false;
        var imageRoot = UI("Slide", body.transform); var rect = imageRoot.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
        rect.anchoredPosition = new Vector2(0, 65.5f); rect.sizeDelta = StatusWindowLayout.ReferenceSize;
        var image = imageRoot.AddComponent<Image>(); image.preserveAspect = true; image.raycastTarget = true;
        var previous = Button("Previous", body.transform, Sprite("button_left.png"), Vector2.one * .5f, new Vector2(168, 231));
        previous.GetComponent<RectTransform>().anchoredPosition = new Vector2(-888, 65.5f);
        var next = Button("Next", body.transform, Sprite("button_right.png"), Vector2.one * .5f, new Vector2(168, 231));
        next.GetComponent<RectTransform>().anchoredPosition = new Vector2(888, 65.5f);
        var close = Button("Close", body.transform, Sprite("button_close.png"), Vector2.one * .5f, new Vector2(308, 166));
        var closeRect = close.GetComponent<RectTransform>(); closeRect.anchoredPosition = new Vector2(0, -443.5f);
        body.AddComponent<TutorialWindowLayout>().Configure(rect, previous.GetComponent<RectTransform>(), next.GetComponent<RectTransform>(), closeRect, backgroundRect);
        var view = root.AddComponent<TutorialOverlayUI>(); view.Configure(body, image, previous, next, close);
        var saved = PrefabUtility.SaveAsPrefabAsset(root, OverlayPath); Object.DestroyImmediate(root); return saved.GetComponent<TutorialOverlayUI>();
    }
    private static GameObject BuildHelp()
    {
        var root = UI("TutorialHelpButton", null); root.GetComponent<RectTransform>().sizeDelta = new Vector2(64, 64);
        var image = root.AddComponent<Image>(); image.sprite = Sprite("button_normal.png"); image.preserveAspect = true;
        var button = root.AddComponent<Button>(); button.targetGraphic = image; button.transition = Selectable.Transition.None; DisableNavigation(button);
        var launcher = root.AddComponent<TutorialLauncherUI>(); launcher.Configure(null, overlay, Sprite("button_normal.png"), Sprite("button_on_mouse.png"));
        var saved = PrefabUtility.SaveAsPrefabAsset(root, HelpPath); Object.DestroyImmediate(root); return saved;
    }
    private static Button Button(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 size)
    {
        var root = UI(name, parent); var rect = root.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = Vector2.one * .5f; rect.sizeDelta = size;
        var image = root.AddComponent<Image>(); image.sprite = sprite; image.preserveAspect = true;
        var button = root.AddComponent<Button>(); button.targetGraphic = image; button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors; colors.normalColor = colors.highlightedColor = colors.selectedColor = Color.white;
        colors.pressedColor = new Color(.85f, .85f, .85f); colors.disabledColor = new Color(1, 1, 1, .3f); button.colors = colors;
        DisableNavigation(button); return button;
    }
    private static void DisableNavigation(Selectable control) { var navigation = control.navigation; navigation.mode = Navigation.Mode.None; control.navigation = navigation; }
    private static GameObject UI(string name, Transform parent)
    { var root = new GameObject(name, typeof(RectTransform)); root.layer = 5; if (parent != null) root.transform.SetParent(parent, false); return root; }
    private static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static Sprite Sprite(string name)
    { return AssetDatabase.LoadAllAssetsAtPath(Art + name).OfType<Sprite>().FirstOrDefault() ?? throw new InvalidOperationException("Missing tutorial art: " + name); }
}
