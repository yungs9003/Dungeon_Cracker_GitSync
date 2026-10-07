using System;
using System.IO;
using System.Linq;
using Dungeon.Core;
using Dungeon.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ModuleSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Module.unity";
    private const string SlotPrefabPath = "Assets/Prefabs/UI/ModuleSlot.prefab";
    private const string EmptySlotSpritePath = "Assets/Sprites/UI/Module/Group 80.png";
    private const string DatabasePath = "Assets/Resources/Data/db.json";
    private const string ModuleIconRoot = "Assets/Resources/UI/ModuleIcons";
    private const string BackgroundSpritePath = "Assets/Sprites/UI/Module/ModuleMaintenance_Background.png";
    private const string HeadingFontPath = "Assets/Fonts/Cafe24Dangdanghae-v2.0 SDF.asset";
    private const string BodyFontPath = "Assets/Fonts/Cafe24Danjunghae-v2.0 SDF.asset";
    private const string DefaultPreviewPath = "Module_scene_preview.png";
    private const string AlternatePreviewPath = "Module_scene_preview_flight.png";

    private static readonly Vector2 SlotSize = new(176f, 176f);
    private static readonly Vector2 InventorySlotSize = SlotSize * 1.3f;
    private static readonly Color DetailTextColor = new(0.015f, 0.02f, 0.012f, 1f);
    private static readonly Color SelectionColor = new(0.25f, 0.88f, 1f, 0.95f);

    private static readonly Vector2[] EquippedSlotCenters =
    {
        new(210.39f, 261.45f),
        new(702.39f, 273.48f),
        new(210.39f, 508.01f),
        new(702.39f, 520.04f),
        new(210.39f, 742.54f),
        new(702.39f, 754.57f)
    };

    private static readonly string[] EquippedSlotNames =
    {
        "EquipSlot_LT", "EquipSlot_RT", "EquipSlot_LM",
        "EquipSlot_RM", "EquipSlot_LB", "EquipSlot_RB"
    };

    private static readonly Vector2[] InventorySlotCenters =
    {
        new(1127.67f, 600f), new(1346.07f, 600f),
        new(1564.47f, 600f), new(1782.87f, 600f),
        new(1236.87f, 788.37f), new(1455.27f, 788.37f), new(1673.67f, 788.37f)
    };

    [MenuItem("Tools/Dungeon/Rebuild Module Scene UI")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            BuildScene(renderPreviews: false);
            Debug.Log("[ModuleSceneBuilder] SVG-based Module scene UI rebuilt.");
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
            BuildScene(renderPreviews: true);
            Debug.Log("[ModuleSceneBuilder] SVG-based Module scene UI rebuilt and validated.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void BuildScene(bool renderPreviews)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Sprite emptySlot = LoadSprite(EmptySlotSpritePath);
        ModuleDefinition[] inventoryModules = LoadInventoryModules();
        ModuleDefinition initialModule = inventoryModules[0];
        Sprite initialIcon = LoadModuleIcon(initialModule.id);
        Sprite background = LoadSprite(BackgroundSpritePath);
        TMP_FontAsset headingFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HeadingFontPath);
        TMP_FontAsset bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);

        EnsureRequiredAsset(emptySlot, EmptySlotSpritePath);
        EnsureRequiredAsset(background, BackgroundSpritePath);
        EnsureRequiredAsset(headingFont, HeadingFontPath);
        EnsureRequiredAsset(bodyFont, BodyFontPath);

        GameObject slotPrefab = BuildSlotPrefab(emptySlot);
        GameObject navigationPrefab = CommonBottomNavigationBuilder.BuildPrefab();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Canvas canvas = FindSceneCanvas(scene);

        if (canvas == null)
            throw new InvalidOperationException("Module scene does not contain the expected Canvas.");

        GameObject pageRoot = GetOrCreateRoot(canvas.transform, "ModulePageRoot", typeof(Image));
        ConfigurePageRoot(pageRoot, background);
        ModulePageUI modulePage = GetOrAdd<ModulePageUI>(pageRoot);
        DestroyChildren(pageRoot.transform);

        GameObject equippedSlots = CreateUIObject("EquippedSlots", pageRoot.transform);
        Stretch(equippedSlots.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        for (int index = 0; index < EquippedSlotCenters.Length; index++)
        {
            GameObject slot = InstantiateSlot(slotPrefab, equippedSlots.transform, EquippedSlotNames[index]);
            SetTopLeftRect(slot.GetComponent<RectTransform>(), SlotSize, EquippedSlotCenters[index]);
            ConfigureSlot(slot, modulePage, null, string.Empty, string.Empty, false);
        }

        GameObject infoContent = CreateUIObject("ModuleInfoArea", pageRoot.transform);
        Stretch(infoContent.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject largeIconObject = CreateImage("SelectedModuleIcon", infoContent.transform, initialIcon, false);
        SetTopLeftRect(largeIconObject.GetComponent<RectTransform>(), new Vector2(264f, 264f), new Vector2(1153.31f, 183.18f));

        TextMeshProUGUI nameText = CreateText(
            "ModuleNameText",
            infoContent.transform,
            initialModule.name,
            headingFont,
            72f,
            DetailTextColor,
            TextAlignmentOptions.Left);
        SetTopLeftRect(nameText.rectTransform, new Vector2(555f, 92f), new Vector2(1604f, 140f));
        nameText.fontStyle = FontStyles.Bold;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 36f;
        nameText.fontSizeMax = 72f;
        nameText.overflowMode = TextOverflowModes.Truncate;

        GameObject divider = CreateImage("DescriptionDivider", infoContent.transform, null, false);
        divider.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.92f);
        SetTopLeftRect(divider.GetComponent<RectTransform>(), new Vector2(545f, 4f), new Vector2(1601.6f, 193.1f));

        TextMeshProUGUI descriptionText = CreateText(
            "DescriptionText",
            infoContent.transform,
            initialModule.desc,
            bodyFont,
            42f,
            DetailTextColor,
            TextAlignmentOptions.TopLeft);
        SetTopLeftRect(descriptionText.rectTransform, new Vector2(545f, 176f), new Vector2(1602f, 292f));
        descriptionText.textWrappingMode = TextWrappingModes.Normal;
        descriptionText.enableAutoSizing = true;
        descriptionText.fontSizeMin = 26f;
        descriptionText.fontSizeMax = 42f;
        descriptionText.lineSpacing = -8f;
        descriptionText.overflowMode = TextOverflowModes.Truncate;

        GameObject inventoryArea = CreateUIObject("ModuleInventoryArea", pageRoot.transform);
        Stretch(inventoryArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        ModuleSlotUI alternateSelection = null;
        ModuleSlotUI defaultSelection = null;

        for (int index = 0; index < InventorySlotCenters.Length; index++)
        {
            int slotNumber = index + 1;
            GameObject slot = InstantiateSlot(slotPrefab, inventoryArea.transform, $"Slot{slotNumber:00}");
            SetTopLeftRect(slot.GetComponent<RectTransform>(), InventorySlotSize, InventorySlotCenters[index]);

            ModuleDefinition definition = index < inventoryModules.Length ? inventoryModules[index] : null;
            Sprite icon = definition != null ? LoadModuleIcon(definition.id) : null;
            ModuleSlotUI slotUI = ConfigureSlot(
                slot, modulePage, icon, definition?.name, definition?.desc, definition != null);
            slotUI.ConfigureRuntime(modulePage, definition, icon, ModuleSlotRole.Inventory, 0);
            EditorUtility.SetDirty(slotUI);

            if (slotNumber == 1)
                defaultSelection = slotUI;
            else if (slotNumber == 2)
                alternateSelection = slotUI;
        }

        modulePage.Configure(
            infoContent,
            largeIconObject.GetComponent<Image>(),
            nameText,
            descriptionText,
            defaultSelection,
            showInitialSelection: true);

        if (defaultSelection == null || alternateSelection == null)
            throw new InvalidOperationException("The database module selections were not created.");

        defaultSelection.Select();

        GameObject bottomNavigation = CommonBottomNavigationBuilder.ReplaceInScene(
            scene,
            navigationPrefab,
            scene.name);
        ValidateBuiltUI(pageRoot, bottomNavigation, modulePage);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (renderPreviews)
        {
            RenderCanvasPreview(canvas, DefaultPreviewPath);
            alternateSelection.Select();
            Canvas.ForceUpdateCanvases();
            RenderCanvasPreview(canvas, AlternatePreviewPath);

            defaultSelection.Select();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
    }

    private static void ConfigurePageRoot(GameObject pageRoot, Sprite background)
    {
        RectTransform pageRect = pageRoot.GetComponent<RectTransform>();
        Stretch(pageRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        pageRect.SetSiblingIndex(0);

        foreach (LayoutGroup layout in pageRoot.GetComponents<LayoutGroup>())
            UnityEngine.Object.DestroyImmediate(layout);

        Image pageBackground = GetOrAdd<Image>(pageRoot);
        pageBackground.sprite = background;
        pageBackground.type = Image.Type.Simple;
        pageBackground.preserveAspect = false;
        pageBackground.color = Color.white;
        pageBackground.raycastTarget = false;
    }

    private static GameObject BuildSlotPrefab(Sprite emptySlot)
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/UI");

        GameObject root = CreateUIObject(
            "ModuleSlot",
            null,
            typeof(Image),
            typeof(Button),
            typeof(Outline),
            typeof(ModuleSlotUI));

        root.GetComponent<RectTransform>().sizeDelta = SlotSize;

        Image frame = root.GetComponent<Image>();
        frame.sprite = emptySlot;
        frame.type = Image.Type.Simple;
        frame.preserveAspect = true;
        frame.raycastTarget = true;

        Outline outline = root.GetComponent<Outline>();
        outline.effectColor = SelectionColor;
        outline.effectDistance = new Vector2(4f, -4f);
        outline.useGraphicAlpha = false;
        outline.enabled = false;

        Button button = root.GetComponent<Button>();
        button.targetGraphic = frame;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.82f, 0.96f, 1f, 1f);
        colors.pressedColor = new Color(0.62f, 0.84f, 1f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.06f;
        button.colors = colors;

        GameObject iconObject = CreateImage("ModuleIcon", root.transform, null, false);
        Stretch(iconObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        iconObject.SetActive(false);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static ModuleSlotUI ConfigureSlot(
        GameObject slot,
        ModulePageUI page,
        Sprite icon,
        string title,
        string body,
        bool selectable)
    {
        ModuleSlotUI slotUI = slot.GetComponent<ModuleSlotUI>();
        Image iconImage = slot.transform.Find("ModuleIcon").GetComponent<Image>();
        Outline outline = slot.GetComponent<Outline>();
        slotUI.Configure(page, iconImage, icon, title, body, selectable, outline);
        EditorUtility.SetDirty(slotUI);
        return slotUI;
    }

    private static void ValidateBuiltUI(GameObject pageRoot, GameObject bottomNavigation, ModulePageUI modulePage)
    {
        ModuleSlotUI[] slots = pageRoot.GetComponentsInChildren<ModuleSlotUI>(true);
        int selectableSlots = slots.Count(slot => slot.IsSelectable);
        BottomNavigationButtonUI[] navigationButtons =
            bottomNavigation.GetComponentsInChildren<BottomNavigationButtonUI>(true);
        int pageButtonCount = pageRoot.GetComponentsInChildren<Button>(true).Length;
        int bottomButtonCount = bottomNavigation.GetComponentsInChildren<Button>(true).Length;

        if (slots.Length != 13)
            throw new InvalidOperationException($"Expected 13 module slots, but found {slots.Length}.");
        if (selectableSlots != 5)
            throw new InvalidOperationException($"Expected 5 selectable inventory modules, but found {selectableSlots}.");
        if (pageButtonCount != 13 || bottomButtonCount != 4)
            throw new InvalidOperationException(
                $"Expected 17 UI buttons (13 module + 4 navigation), but found {pageButtonCount + bottomButtonCount}.");
        if (navigationButtons.Length != 4)
            throw new InvalidOperationException($"Expected 4 scene navigation bindings, but found {navigationButtons.Length}.");
        if (navigationButtons.Any(binding => binding.GetComponent<Button>().onClick.GetPersistentEventCount() != 1))
            throw new InvalidOperationException("A bottom navigation button does not have exactly one persistent scene-load callback.");
        if (navigationButtons.Count(binding => binding.IsCurrentScene) != 1 ||
            !navigationButtons.Any(binding => binding.IsCurrentScene && binding.TargetSceneName == "Module"))
            throw new InvalidOperationException("The common navigation did not mark Module as the current scene.");
        if (modulePage.SelectedSlot == null || modulePage.SelectedSlot.ModuleId != "000")
            throw new InvalidOperationException("The database default module selection was not applied.");

        Debug.Log("[ModuleSceneBuilder] Validation passed: 6 equipped + 7 inventory slots, " +
                  "5 modules with resource icons, 4 scene navigation buttons, module 000 selected.");
    }

    private static Canvas FindSceneCanvas(Scene scene)
    {
        return scene.GetRootGameObjects()
            .Where(root => root.name == "Canvas")
            .Select(root => root.GetComponent<Canvas>())
            .FirstOrDefault(component => component != null);
    }

    private static GameObject GetOrCreateRoot(Transform parent, string objectName, params Type[] components)
    {
        Transform existing = parent.Find(objectName);
        return existing != null ? existing.gameObject : CreateUIObject(objectName, parent, components);
    }

    private static GameObject InstantiateSlot(GameObject prefab, Transform parent, string objectName)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = objectName;
        instance.layer = 5;
        return instance;
    }

    private static TextMeshProUGUI CreateText(
        string objectName,
        Transform parent,
        string text,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUIObject(objectName, parent, typeof(TextMeshProUGUI));
        TextMeshProUGUI textComponent = textObject.GetComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.font = font;
        textComponent.fontSize = fontSize;
        textComponent.color = color;
        textComponent.alignment = alignment;
        textComponent.enableAutoSizing = false;
        textComponent.raycastTarget = false;
        return textComponent;
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

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void SetTopLeftRect(RectTransform rect, Vector2 size, Vector2 centerFromTopLeft)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(centerFromTopLeft.x, -centerFromTopLeft.y);
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

    private static void DestroyChildren(Transform parent)
    {
        for (int index = parent.childCount - 1; index >= 0; index--)
            UnityEngine.Object.DestroyImmediate(parent.GetChild(index).gameObject);
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }

    private static Sprite LoadModuleIcon(string id)
    {
        return LoadSprite($"{ModuleIconRoot}/{GameDatabase.NormalizeId(id)}.png");
    }

    private static ModuleDefinition[] LoadInventoryModules()
    {
        TextAsset databaseAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(DatabasePath);
        EnsureRequiredAsset(databaseAsset, DatabasePath);
        DbRoot data = JsonUtility.FromJson<DbRoot>(databaseAsset.text);
        ModuleDefinition[] modules = (data.modules ?? Array.Empty<ModuleDefinition>())
            .Where(module => module != null && LoadModuleIcon(module.id) != null)
            .OrderBy(module => module.id, StringComparer.Ordinal)
            .ToArray();

        if (modules.Length != 5)
            throw new InvalidOperationException($"Expected 5 modules with resource icons, but found {modules.Length}.");

        return modules;
    }

    private static void EnsureRequiredAsset(UnityEngine.Object asset, string path)
    {
        if (asset == null)
            throw new FileNotFoundException($"Required Module UI asset was not found: {path}", path);
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

    private static void RenderCanvasPreview(Canvas canvas, string relativeOutputPath)
    {
        const int width = 1920;
        const int height = 1080;

        Canvas[] sceneCanvases = canvas.gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
            .Where(candidate => candidate.isRootCanvas)
            .ToArray();
        RenderMode[] originalModes = sceneCanvases.Select(candidate => candidate.renderMode).ToArray();
        Camera[] originalCameras = sceneCanvases.Select(candidate => candidate.worldCamera).ToArray();
        float[] originalPlaneDistances = sceneCanvases.Select(candidate => candidate.planeDistance).ToArray();

        GameObject cameraObject = new("ModulePreviewCamera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.orthographicSize = height * 0.5f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 2000f;
        camera.transform.position = new Vector3(0f, 0f, -1000f);
        camera.cullingMask = 1 << 5;

        RenderTexture target = new(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D screenshot = new(width, height, TextureFormat.RGBA32, false);

        try
        {
            camera.targetTexture = target;

            foreach (Canvas sceneCanvas in sceneCanvases)
            {
                sceneCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                sceneCanvas.worldCamera = camera;
                sceneCanvas.planeDistance = 100f;
            }

            Canvas.ForceUpdateCanvases();

            camera.Render();
            RenderTexture.active = target;
            screenshot.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            screenshot.Apply();

            string outputPath = Path.GetFullPath(relativeOutputPath);
            File.WriteAllBytes(outputPath, screenshot.EncodeToPNG());
            Debug.Log($"[ModuleSceneBuilder] Preview saved: {outputPath}");
        }
        finally
        {
            RenderTexture.active = null;
            camera.targetTexture = null;

            for (int index = 0; index < sceneCanvases.Length; index++)
            {
                sceneCanvases[index].renderMode = originalModes[index];
                sceneCanvases[index].worldCamera = originalCameras[index];
                sceneCanvases[index].planeDistance = originalPlaneDistances[index];
            }

            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(screenshot);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
}
