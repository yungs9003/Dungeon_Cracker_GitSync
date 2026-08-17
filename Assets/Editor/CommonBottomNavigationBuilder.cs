using System;
using System.IO;
using System.Linq;
using Dungeon.System;
using Dungeon.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CommonBottomNavigationBuilder
{
    public const string PrefabPath = "Assets/Prefabs/UI/BottomNavigationBar.prefab";

    private const string FrameSpritePath = "Assets/Sprites/UI/Module/menu_frame.png";
    private const string ScanLinesSpritePath = "Assets/Sprites/UI/Module/scan_lines_background.png";
    private const string SeparatorsSpritePath = "Assets/Sprites/UI/Module/menu_split.png";
    private const string HighlightSpritePath = "Assets/Sprites/UI/Module/highlight.png";
    private const string WorldMapIconPath = "Assets/Sprites/UI/Module/worldmap_icon.png";
    private const string ShopIconPath = "Assets/Sprites/UI/Module/market_icon.png";
    private const string GuildIconPath = "Assets/Sprites/UI/Module/guild_icon.png";
    private const string ModuleIconPath = "Assets/Sprites/UI/Module/module_icon.png";
    private const string FontPath = "Assets/Fonts/Cafe24Dangdanghae-v2.0 SDF.asset";
    private const string IconTintShaderPath = "Assets/Shaders/UI/BottomNavigationAlphaTint.shader";
    private const string IconTintMaterialPath = "Assets/Materials/UI/BottomNavigationIconTint.mat";

    private const int ReferenceWidth = 1920;
    private const int ReferenceHeight = 1080;
    private const float PanelHeight = 131f;

    private static readonly SceneSpec[] SceneSpecs =
    {
        new("Assets/Scenes/WorldMap.unity", "WorldMap"),
        new("Assets/Scenes/Shop.unity", "Shop"),
        new("Assets/Scenes/Guild.unity", "Guild"),
        new("Assets/Scenes/Module.unity", "Module")
    };

    private static readonly ButtonSpec[] ButtonSpecs =
    {
        new("WorldMapButton", "WorldMap", "월드맵", WorldMapIconPath, 238.13f, 689.25f, -125f),
        new("ShopButton", "Shop", "상점", ShopIconPath, 689.25f, 1076.99f, -105f),
        new("GuildButton", "Guild", "길드", GuildIconPath, 1076.99f, 1495.73f, -94f),
        new("ModuleButton", "Module", "모듈", ModuleIconPath, 1495.73f, 1920f, -105f)
    };

    [MenuItem("Tools/Dungeon/Rebuild Common Bottom Navigation")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            BuildAndApplyToAllScenes(renderPreviews: false);
            Debug.Log("[CommonBottomNavigationBuilder] Common bottom navigation rebuilt in four scenes.");
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }

    public static void BuildFromCommandLine()
    {
        try
        {
            BuildAndApplyToAllScenes(renderPreviews: true);
            Debug.Log("[CommonBottomNavigationBuilder] Build, scene application, and previews completed.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static GameObject BuildPrefab()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Sprite frame = LoadSprite(FrameSpritePath);
        Sprite scanLines = LoadSprite(ScanLinesSpritePath);
        Sprite separators = LoadSprite(SeparatorsSpritePath);
        Sprite highlight = LoadSprite(HighlightSpritePath);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Shader iconTintShader = AssetDatabase.LoadAssetAtPath<Shader>(IconTintShaderPath);

        EnsureRequiredAsset(frame, FrameSpritePath);
        EnsureRequiredAsset(scanLines, ScanLinesSpritePath);
        EnsureRequiredAsset(separators, SeparatorsSpritePath);
        EnsureRequiredAsset(highlight, HighlightSpritePath);
        EnsureRequiredAsset(font, FontPath);
        EnsureRequiredAsset(iconTintShader, IconTintShaderPath);

        EnsureFolder("Assets/Materials");
        EnsureFolder("Assets/Materials/UI");
        Material iconTintMaterial = EnsureIconTintMaterial(iconTintShader);
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/UI");

        GameObject canvasRoot = CreateUIObject(
            "BottomNavigationCanvas",
            null,
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(SceneButtonLoader));

        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        // Main content is order 0 and the shared Status window is order 3.
        // Keep navigation above scene content but below modal/status UI.
        canvas.sortingOrder = 1;
        canvas.targetDisplay = 0;

        CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;

        GraphicRaycaster raycaster = canvasRoot.GetComponent<GraphicRaycaster>();
        raycaster.ignoreReversedGraphics = true;
        raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

        SceneButtonLoader loader = canvasRoot.GetComponent<SceneButtonLoader>();

        GameObject panel = CreateUIObject("BottomPanel", canvasRoot.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(0f, PanelHeight);

        GameObject scanLinesObject = CreateImage("ScanLinesBackground", panel.transform, scanLines, false);
        SetBottomLeftRect(
            scanLinesObject.GetComponent<RectTransform>(),
            new Vector2(1686f, 112f),
            new Vector2(238f, 0f),
            Vector2.zero);
        scanLinesObject.GetComponent<Image>().preserveAspect = false;

        GameObject frameObject = CreateImage("Frame", panel.transform, frame, false);
        Stretch(frameObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        frameObject.GetComponent<Image>().preserveAspect = false;

        foreach (ButtonSpec spec in ButtonSpecs)
        {
            Sprite icon = LoadSprite(spec.IconPath);
            EnsureRequiredAsset(icon, spec.IconPath);
            CreateNavigationButton(panel.transform, loader, spec, icon, highlight, font, iconTintMaterial);
        }

        GameObject separatorsObject = CreateImage("Separators", panel.transform, separators, false);
        SetBottomLeftRect(
            separatorsObject.GetComponent<RectTransform>(),
            new Vector2(811f, 79f),
            new Vector2(689.25f, 12f),
            Vector2.zero);
        separatorsObject.GetComponent<Image>().preserveAspect = false;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(canvasRoot, PrefabPath);
        UnityEngine.Object.DestroyImmediate(canvasRoot);

        ValidatePrefab(prefab);
        AssetDatabase.SaveAssets();
        return prefab;
    }

    public static GameObject ReplaceInScene(Scene scene, GameObject prefab, string currentSceneName = null)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            throw new ArgumentException("A loaded scene is required.", nameof(scene));
        if (prefab == null)
            throw new ArgumentNullException(nameof(prefab));

        foreach (GameObject root in scene.GetRootGameObjects().Where(IsCommonNavigationRoot).ToArray())
            UnityEngine.Object.DestroyImmediate(root);

        GameObject mainCanvasRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Canvas");
        Transform legacyBottomPanel = mainCanvasRoot != null
            ? mainCanvasRoot.transform.Find("BottomPanel")
            : null;

        if (legacyBottomPanel != null)
            UnityEngine.Object.DestroyImmediate(legacyBottomPanel.gameObject);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "BottomNavigationCanvas";
        ApplyEditorSceneState(instance, currentSceneName ?? scene.name);
        return instance;
    }

    private static void BuildAndApplyToAllScenes(bool renderPreviews)
    {
        GameObject prefab = BuildPrefab();

        foreach (SceneSpec spec in SceneSpecs)
        {
            Scene scene = EditorSceneManager.OpenScene(spec.Path, OpenSceneMode.Single);
            GameObject instance = ReplaceInScene(scene, prefab, spec.SceneName);
            ValidateSceneNavigation(scene, instance, spec.SceneName);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (renderPreviews)
        {
            foreach (SceneSpec spec in SceneSpecs)
                RenderNavigationPreview(prefab, spec.SceneName, null, $"BottomNavigation_{spec.SceneName}.png");

            RenderNavigationPreview(
                prefab,
                "Module",
                "WorldMap",
                "BottomNavigation_Module_HoverWorldMap.png");
        }
    }

    private static void CreateNavigationButton(
        Transform parent,
        SceneButtonLoader loader,
        ButtonSpec spec,
        Sprite iconSprite,
        Sprite highlightSprite,
        TMP_FontAsset font,
        Material iconTintMaterial)
    {
        GameObject buttonObject = CreateUIObject(
            spec.ObjectName,
            parent,
            typeof(Image),
            typeof(Button),
            typeof(LoadSceneByNameButton),
            typeof(BottomNavigationButtonUI));

        float width = spec.Right - spec.Left;
        SetBottomLeftRect(
            buttonObject.GetComponent<RectTransform>(),
            new Vector2(width, 112f),
            new Vector2((spec.Left + spec.Right) * 0.5f, 0f),
            new Vector2(0.5f, 0f));

        Image raycastImage = buttonObject.GetComponent<Image>();
        raycastImage.sprite = null;
        raycastImage.color = Color.clear;
        raycastImage.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = raycastImage;
        button.transition = Selectable.Transition.None;

        GameObject highlightObject = CreateImage(
            $"{spec.SceneName}Highlight",
            buttonObject.transform,
            highlightSprite,
            false);
        RectTransform highlightRect = highlightObject.GetComponent<RectTransform>();
        highlightRect.anchorMin = highlightRect.anchorMax = new Vector2(0.5f, 0f);
        highlightRect.pivot = new Vector2(0.5f, 0f);
        highlightRect.anchoredPosition = Vector2.zero;
        highlightRect.sizeDelta = highlightSprite.rect.size;
        highlightObject.GetComponent<Image>().preserveAspect = true;
        highlightObject.SetActive(false);

        GameObject content = CreateUIObject($"{spec.SceneName}Content", buttonObject.transform);
        Stretch(content.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject iconObject = CreateImage($"{spec.SceneName}Icon", content.transform, iconSprite, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        SetCenteredRect(iconRect, iconSprite.rect.size, new Vector2(spec.IconOffsetX, 0f));
        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.material = iconTintMaterial;
        iconImage.color = new Color(0.553f, 0.553f, 0.553f, 1f);

        TextMeshProUGUI label = CreateText(
            $"{spec.SceneName}Label",
            content.transform,
            spec.Label,
            font,
            58f,
            new Color(0.553f, 0.553f, 0.553f, 1f));
        SetCenteredRect(label.rectTransform, new Vector2(235f, 104f), new Vector2(75f, -1f));

        LoadSceneByNameButton navigation = buttonObject.GetComponent<LoadSceneByNameButton>();
        navigation.Configure(loader, spec.SceneName, button);
        UnityEventTools.AddPersistentListener(button.onClick, navigation.Go);

        BottomNavigationButtonUI visual = buttonObject.GetComponent<BottomNavigationButtonUI>();
        visual.Configure(navigation, button, iconImage, label, highlightObject.GetComponent<Image>(), content.GetComponent<RectTransform>());
        visual.ApplySceneState(string.Empty);

        EditorUtility.SetDirty(button);
        EditorUtility.SetDirty(navigation);
        EditorUtility.SetDirty(visual);
    }

    private static void ApplyEditorSceneState(GameObject navigationRoot, string currentSceneName)
    {
        foreach (BottomNavigationButtonUI view in navigationRoot.GetComponentsInChildren<BottomNavigationButtonUI>(true))
        {
            view.ApplySceneState(currentSceneName);

            foreach (Component component in view.GetComponentsInChildren<Component>(true))
            {
                EditorUtility.SetDirty(component);
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }

            foreach (Transform child in view.GetComponentsInChildren<Transform>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
        }
    }

    private static Material EnsureIconTintMaterial(Shader shader)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(IconTintMaterialPath);

        if (material == null)
        {
            material = new Material(shader)
            {
                name = "BottomNavigationIconTint"
            };
            AssetDatabase.CreateAsset(material, IconTintMaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
            EditorUtility.SetDirty(material);
        }

        return material;
    }

    private static void ValidatePrefab(GameObject prefab)
    {
        if (prefab == null)
            throw new InvalidOperationException("Bottom navigation prefab was not created.");
        if (prefab.GetComponent<Canvas>() == null || prefab.GetComponent<CanvasScaler>() == null)
            throw new InvalidOperationException("Bottom navigation prefab must own its Canvas and CanvasScaler.");

        BottomNavigationButtonUI[] views = prefab.GetComponentsInChildren<BottomNavigationButtonUI>(true);
        if (views.Length != 4)
            throw new InvalidOperationException($"Expected 4 navigation views, but found {views.Length}.");
        if (views.Select(view => view.TargetSceneName).Distinct().Count() != 4)
            throw new InvalidOperationException("Navigation target scene names must be unique.");
        if (views.Any(view => view.GetComponent<Button>().onClick.GetPersistentEventCount() != 1))
            throw new InvalidOperationException("Each navigation button must have exactly one persistent load callback.");
    }

    private static void ValidateSceneNavigation(Scene scene, GameObject navigationRoot, string currentSceneName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        if (roots.Count(IsCommonNavigationRoot) != 1)
            throw new InvalidOperationException($"{scene.name} must contain exactly one BottomNavigationCanvas.");

        GameObject mainCanvasRoot = roots.FirstOrDefault(root => root.name == "Canvas");
        if (mainCanvasRoot != null && mainCanvasRoot.transform.Find("BottomPanel") != null)
            throw new InvalidOperationException($"{scene.name} still contains a legacy Canvas/BottomPanel.");

        BottomNavigationButtonUI[] views = navigationRoot.GetComponentsInChildren<BottomNavigationButtonUI>(true);
        if (views.Length != 4)
            throw new InvalidOperationException($"{scene.name} contains {views.Length} navigation buttons instead of 4.");
        if (views.Count(view => view.IsCurrentScene) != 1)
            throw new InvalidOperationException($"{scene.name} must have exactly one current-scene navigation state.");
        if (!views.Any(view => view.IsCurrentScene && view.TargetSceneName == currentSceneName))
            throw new InvalidOperationException($"{scene.name} did not activate its matching navigation button.");

        Debug.Log($"[CommonBottomNavigationBuilder] {scene.name}: shared prefab and current-scene state validated.");
    }

    private static bool IsCommonNavigationRoot(GameObject root)
    {
        if (root == null)
            return false;

        GameObject prefabSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(root);
        if (prefabSource != null && AssetDatabase.GetAssetPath(prefabSource) == PrefabPath)
            return true;

        if (root.GetComponentsInChildren<BottomNavigationButtonUI>(true).Length == ButtonSpecs.Length)
            return true;

        return root.name == "BottomNavigationCanvas";
    }

    private static void RenderNavigationPreview(
        GameObject prefab,
        string currentSceneName,
        string hoverSceneName,
        string relativeOutputPath)
    {
        Scene previewScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, previewScene);
        BottomNavigationButtonUI[] views = instance.GetComponentsInChildren<BottomNavigationButtonUI>(true);

        foreach (BottomNavigationButtonUI view in views)
        {
            view.ApplySceneState(currentSceneName);

            if (view.TargetSceneName == hoverSceneName)
                view.OnPointerEnter(null);
        }

        Canvas canvas = instance.GetComponent<Canvas>();
        RenderMode originalMode = canvas.renderMode;
        Camera originalCamera = canvas.worldCamera;
        float originalPlaneDistance = canvas.planeDistance;

        GameObject cameraObject = new("BottomNavigationPreviewCamera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = ReferenceHeight * 0.5f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 2000f;
        camera.transform.position = new Vector3(0f, 0f, -1000f);
        camera.cullingMask = 1 << 5;

        RenderTexture target = new(ReferenceWidth, ReferenceHeight, 24, RenderTextureFormat.ARGB32);
        Texture2D screenshot = new(ReferenceWidth, (int)PanelHeight, TextureFormat.RGBA32, false);

        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100f;
            Canvas.ForceUpdateCanvases();

            camera.Render();
            RenderTexture.active = target;
            screenshot.ReadPixels(new Rect(0f, 0f, ReferenceWidth, PanelHeight), 0, 0);
            screenshot.Apply();

            string outputPath = Path.GetFullPath(relativeOutputPath);
            File.WriteAllBytes(outputPath, screenshot.EncodeToPNG());
            Debug.Log($"[CommonBottomNavigationBuilder] Preview saved: {outputPath}");
        }
        finally
        {
            RenderTexture.active = null;
            camera.targetTexture = null;
            canvas.renderMode = originalMode;
            canvas.worldCamera = originalCamera;
            canvas.planeDistance = originalPlaneDistance;
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(screenshot);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    private static TextMeshProUGUI CreateText(
        string objectName,
        Transform parent,
        string text,
        TMP_FontAsset font,
        float fontSize,
        Color color)
    {
        GameObject textObject = CreateUIObject(objectName, parent, typeof(TextMeshProUGUI));
        TextMeshProUGUI component = textObject.GetComponent<TextMeshProUGUI>();
        component.text = text;
        component.font = font;
        component.fontSize = fontSize;
        component.fontStyle = FontStyles.Bold;
        component.color = color;
        component.alignment = TextAlignmentOptions.Center;
        component.enableAutoSizing = false;
        component.raycastTarget = false;
        component.overflowMode = TextOverflowModes.Overflow;
        return component;
    }

    private static GameObject CreateImage(string objectName, Transform parent, Sprite sprite, bool raycastTarget)
    {
        GameObject imageObject = CreateUIObject(objectName, parent, typeof(Image));
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = sprite != null;
        image.raycastTarget = raycastTarget;
        image.color = Color.white;
        return imageObject;
    }

    private static GameObject CreateUIObject(string objectName, Transform parent, params Type[] additionalComponents)
    {
        Type[] components = new[] { typeof(RectTransform) }
            .Concat(additionalComponents.Where(type => type != typeof(RectTransform)))
            .Distinct()
            .ToArray();

        GameObject gameObject = new(objectName, components);
        gameObject.layer = 5;

        if (parent != null)
            gameObject.transform.SetParent(parent, false);

        return gameObject;
    }

    private static void SetCenteredRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void SetBottomLeftRect(RectTransform rect, Vector2 size, Vector2 position, Vector2 pivot)
    {
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void Stretch(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 insetMin,
        Vector2 insetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = insetMin;
        rect.offsetMax = -insetMax;
        rect.localScale = Vector3.one;
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }

    private static void EnsureRequiredAsset(UnityEngine.Object asset, string path)
    {
        if (asset == null)
            throw new FileNotFoundException($"Required bottom navigation asset was not found: {path}", path);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = Path.GetFileName(path);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }

    private readonly struct SceneSpec
    {
        public SceneSpec(string path, string sceneName)
        {
            Path = path;
            SceneName = sceneName;
        }

        public string Path { get; }
        public string SceneName { get; }
    }

    private readonly struct ButtonSpec
    {
        public ButtonSpec(
            string objectName,
            string sceneName,
            string label,
            string iconPath,
            float left,
            float right,
            float iconOffsetX)
        {
            ObjectName = objectName;
            SceneName = sceneName;
            Label = label;
            IconPath = iconPath;
            Left = left;
            Right = right;
            IconOffsetX = iconOffsetX;
        }

        public string ObjectName { get; }
        public string SceneName { get; }
        public string Label { get; }
        public string IconPath { get; }
        public float Left { get; }
        public float Right { get; }
        public float IconOffsetX { get; }
    }
}
