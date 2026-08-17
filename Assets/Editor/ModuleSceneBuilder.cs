using System;
using System.IO;
using System.Linq;
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
    private const string PhysicalDefenseSpritePath = "Assets/Sprites/UI/Module/Module-DEF.png";
    private const string MagicDefenseSpritePath = "Assets/Sprites/UI/Module/Module-M.DEF.png";
    private const string BackgroundSpritePath = "Assets/Sprites/UI/Module/ModuleMaintenance_Background.png";
    private const string HeadingFontPath = "Assets/Fonts/Cafe24Dangdanghae-v2.0 SDF.asset";
    private const string BodyFontPath = "Assets/Fonts/Cafe24Danjunghae-v2.0 SDF.asset";
    private const string DefaultPreviewPath = "Module_scene_preview.png";
    private const string MagicPreviewPath = "Module_scene_preview_mdef.png";

    private static readonly Vector2 SlotSize = new(176f, 176f);
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
        new(1119.27f, 557.27f), new(1287.27f, 555.60f), new(1455.27f, 555.60f),
        new(1623.27f, 557.27f), new(1791.27f, 557.27f),
        new(1203.27f, 702.17f), new(1371.27f, 702.17f), new(1541.96f, 702.17f),
        new(1712.22f, 702.17f),
        new(1121.96f, 848.97f), new(1287.27f, 848.97f), new(1455.27f, 848.97f),
        new(1625.96f, 848.97f), new(1796.22f, 848.97f)
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
        Sprite physicalDefense = LoadSprite(PhysicalDefenseSpritePath);
        Sprite magicDefense = LoadSprite(MagicDefenseSpritePath);
        Sprite background = LoadSprite(BackgroundSpritePath);
        TMP_FontAsset headingFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HeadingFontPath);
        TMP_FontAsset bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);

        EnsureRequiredAsset(emptySlot, EmptySlotSpritePath);
        EnsureRequiredAsset(physicalDefense, PhysicalDefenseSpritePath);
        EnsureRequiredAsset(magicDefense, MagicDefenseSpritePath);
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

        GameObject largeIconObject = CreateImage("SelectedModuleIcon", infoContent.transform, physicalDefense, false);
        SetTopLeftRect(largeIconObject.GetComponent<RectTransform>(), new Vector2(264f, 264f), new Vector2(1153.31f, 183.18f));

        TextMeshProUGUI nameText = CreateText(
            "ModuleNameText",
            infoContent.transform,
            "물리 내성 증가",
            headingFont,
            72f,
            DetailTextColor,
            TextAlignmentOptions.Left);
        SetTopLeftRect(nameText.rectTransform, new Vector2(555f, 92f), new Vector2(1604f, 140f));
        nameText.fontStyle = FontStyles.Bold;
        nameText.overflowMode = TextOverflowModes.Overflow;

        GameObject divider = CreateImage("DescriptionDivider", infoContent.transform, null, false);
        divider.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.92f);
        SetTopLeftRect(divider.GetComponent<RectTransform>(), new Vector2(545f, 4f), new Vector2(1601.6f, 193.1f));

        TextMeshProUGUI descriptionText = CreateText(
            "DescriptionText",
            infoContent.transform,
            "장착 시, 플레이어의 물리 내성을\n10 증가",
            bodyFont,
            42f,
            DetailTextColor,
            TextAlignmentOptions.TopLeft);
        SetTopLeftRect(descriptionText.rectTransform, new Vector2(545f, 125f), new Vector2(1602f, 276f));
        descriptionText.textWrappingMode = TextWrappingModes.Normal;
        descriptionText.lineSpacing = -8f;
        descriptionText.overflowMode = TextOverflowModes.Overflow;

        GameObject inventoryArea = CreateUIObject("ModuleInventoryArea", pageRoot.transform);
        Stretch(inventoryArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        ModuleSlotUI magicSelection = null;
        ModuleSlotUI defaultSelection = null;

        for (int index = 0; index < InventorySlotCenters.Length; index++)
        {
            int slotNumber = index + 1;
            GameObject slot = InstantiateSlot(slotPrefab, inventoryArea.transform, $"Slot{slotNumber:00}");
            SetTopLeftRect(slot.GetComponent<RectTransform>(), SlotSize, InventorySlotCenters[index]);

            Sprite icon = null;
            string title = string.Empty;
            string body = string.Empty;
            bool selectable = false;

            if (slotNumber == 1)
            {
                icon = magicDefense;
                title = "마법 내성 증가";
                body = "장착 시, 플레이어의 마법 내성을\n10 증가";
                selectable = true;
            }
            else if (slotNumber == 2)
            {
                icon = physicalDefense;
                title = "물리 내성 증가";
                body = "장착 시, 플레이어의 물리 내성을\n10 증가";
                selectable = true;
            }

            ModuleSlotUI slotUI = ConfigureSlot(slot, modulePage, icon, title, body, selectable);

            if (slotNumber == 1)
                magicSelection = slotUI;
            else if (slotNumber == 2)
                defaultSelection = slotUI;
        }

        modulePage.Configure(
            infoContent,
            largeIconObject.GetComponent<Image>(),
            nameText,
            descriptionText,
            defaultSelection,
            showInitialSelection: true);

        if (defaultSelection == null || magicSelection == null)
            throw new InvalidOperationException("The two SVG module selections were not created.");

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
            magicSelection.Select();
            Canvas.ForceUpdateCanvases();
            RenderCanvasPreview(canvas, MagicPreviewPath);

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

        if (slots.Length != 20)
            throw new InvalidOperationException($"Expected 20 module slots, but found {slots.Length}.");
        if (selectableSlots != 2)
            throw new InvalidOperationException($"Expected 2 selectable inventory modules, but found {selectableSlots}.");
        if (pageButtonCount != 20 || bottomButtonCount != 4)
            throw new InvalidOperationException(
                $"Expected 24 UI buttons (20 module + 4 navigation), but found {pageButtonCount + bottomButtonCount}.");
        if (navigationButtons.Length != 4)
            throw new InvalidOperationException($"Expected 4 scene navigation bindings, but found {navigationButtons.Length}.");
        if (navigationButtons.Any(binding => binding.GetComponent<Button>().onClick.GetPersistentEventCount() != 1))
            throw new InvalidOperationException("A bottom navigation button does not have exactly one persistent scene-load callback.");
        if (navigationButtons.Count(binding => binding.IsCurrentScene) != 1 ||
            !navigationButtons.Any(binding => binding.IsCurrentScene && binding.TargetSceneName == "Module"))
            throw new InvalidOperationException("The common navigation did not mark Module as the current scene.");
        if (modulePage.SelectedSlot == null || modulePage.SelectedSlot.ModuleName != "물리 내성 증가")
            throw new InvalidOperationException("The SVG default DEF selection was not applied.");

        Debug.Log("[ModuleSceneBuilder] Validation passed: 20 module slots, 2 selectable modules, " +
                  "4 scene navigation buttons, DEF default selection.");
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
