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
    private const string ArmorSpritePath = "Assets/Sprites/UI/Module/IMG_4899 1.png";
    private const string PhysicalDefenseSpritePath = "Assets/Sprites/UI/Module/Module-DEF.png";
    private const string MagicDefenseSpritePath = "Assets/Sprites/UI/Module/Module-M.DEF.png";
    private const string VitalitySpritePath = "Assets/Sprites/UI/Module/Module-DEF (1).png";
    private const string HeadingFontPath = "Assets/Fonts/Cafe24Dangdanghae-v2.0 SDF.asset";
    private const string BodyFontPath = "Assets/Fonts/Cafe24Danjunghae-v2.0 SDF.asset";
    private const string PreviewPath = "Module_scene_preview.png";

    private static readonly Color TextColor = new(0.08f, 0.09f, 0.11f, 1f);
    private static readonly Color MutedTextColor = new(0.28f, 0.31f, 0.36f, 1f);
    private static readonly Color DividerColor = new(0.12f, 0.13f, 0.15f, 0.9f);
    private static readonly Color CardColor = new(0.97f, 0.975f, 0.985f, 0.78f);
    private static readonly Color AreaDividerColor = new(0.15f, 0.17f, 0.2f, 0.14f);

    [MenuItem("Tools/Dungeon/Rebuild Module Scene UI")]
    public static void BuildFromMenu()
    {
        BuildScene(renderPreview: false);
        Debug.Log("[ModuleSceneBuilder] Module scene UI rebuilt.");
    }

    public static void BuildFromCommandLine()
    {
        try
        {
            BuildScene(renderPreview: true);
            Debug.Log("[ModuleSceneBuilder] Module scene UI rebuilt and preview rendered.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void BuildScene(bool renderPreview)
    {
        Sprite emptySlot = LoadSprite(EmptySlotSpritePath);
        Sprite armor = LoadSprite(ArmorSpritePath);
        Sprite physicalDefense = LoadSprite(PhysicalDefenseSpritePath);
        Sprite magicDefense = LoadSprite(MagicDefenseSpritePath);
        Sprite vitality = LoadSprite(VitalitySpritePath);
        TMP_FontAsset headingFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HeadingFontPath);
        TMP_FontAsset bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);

        EnsureRequiredAsset(emptySlot, EmptySlotSpritePath);
        EnsureRequiredAsset(armor, ArmorSpritePath);
        EnsureRequiredAsset(physicalDefense, PhysicalDefenseSpritePath);
        EnsureRequiredAsset(magicDefense, MagicDefenseSpritePath);
        EnsureRequiredAsset(vitality, VitalitySpritePath);
        EnsureRequiredAsset(headingFont, HeadingFontPath);
        EnsureRequiredAsset(bodyFont, BodyFontPath);

        GameObject slotPrefab = BuildSlotPrefab(emptySlot);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
            throw new InvalidOperationException("Module scene does not contain a Canvas.");

        Transform existingRoot = canvas.transform.Find("ModulePageRoot");
        GameObject pageRoot = existingRoot != null
            ? existingRoot.gameObject
            : CreateUIObject("ModulePageRoot", canvas.transform, typeof(Image));

        RectTransform pageRect = pageRoot.GetComponent<RectTransform>();
        Stretch(pageRect, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, 80f));
        pageRect.SetSiblingIndex(0);

        Image pageBackground = GetOrAdd<Image>(pageRoot);
        pageBackground.sprite = null;
        pageBackground.color = Color.white;
        pageBackground.raycastTarget = false;

        ModulePageUI modulePage = GetOrAdd<ModulePageUI>(pageRoot);
        DestroyChildren(pageRoot.transform);

        GameObject leftArea = CreateUIObject("LeftEquipArea", pageRoot.transform);
        RectTransform leftRect = leftArea.GetComponent<RectTransform>();
        Stretch(leftRect, Vector2.zero, new Vector2(0.43f, 1f), Vector2.zero, Vector2.zero);

        GameObject armorObject = CreateImage("ArmorImage", leftArea.transform, armor, false);
        SetCenteredRect(armorObject.GetComponent<RectTransform>(), new Vector2(1290f, 860f), new Vector2(280f, 0f));

        GameObject equippedSlots = CreateUIObject("EquippedSlots", leftArea.transform);
        Stretch(equippedSlots.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        Vector2[] equippedPositions =
        {
            new(-245f, 270f), new(245f, 270f),
            new(-245f, 0f), new(245f, 0f),
            new(-245f, -270f), new(245f, -270f)
        };
        string[] equippedNames =
        {
            "EquipSlot_LT", "EquipSlot_RT", "EquipSlot_LM",
            "EquipSlot_RM", "EquipSlot_LB", "EquipSlot_RB"
        };

        for (int i = 0; i < equippedPositions.Length; i++)
        {
            GameObject slot = InstantiateSlot(slotPrefab, equippedSlots.transform, equippedNames[i]);
            SetCenteredRect(slot.GetComponent<RectTransform>(), new Vector2(170f, 170f), equippedPositions[i]);
            ConfigureSlot(slot, modulePage, null, string.Empty, string.Empty, false);
        }

        GameObject verticalDivider = CreateImage("VerticalDivider", pageRoot.transform, null, false);
        RectTransform verticalDividerRect = verticalDivider.GetComponent<RectTransform>();
        verticalDividerRect.anchorMin = new Vector2(0.43f, 0.05f);
        verticalDividerRect.anchorMax = new Vector2(0.43f, 0.95f);
        verticalDividerRect.pivot = new Vector2(0.5f, 0.5f);
        verticalDividerRect.anchoredPosition = Vector2.zero;
        verticalDividerRect.sizeDelta = new Vector2(2f, 0f);
        verticalDivider.GetComponent<Image>().color = AreaDividerColor;

        GameObject rightArea = CreateUIObject("RightArea", pageRoot.transform);
        Stretch(rightArea.GetComponent<RectTransform>(), new Vector2(0.43f, 0f), Vector2.one, Vector2.zero, Vector2.zero);

        GameObject infoArea = CreatePanel("ModuleInfoArea", rightArea.transform, CardColor);
        Stretch(infoArea.GetComponent<RectTransform>(), new Vector2(0.04f, 0.56f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero);

        GameObject infoContent = CreateUIObject("InfoContent", infoArea.transform);
        Stretch(infoContent.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject largeIconObject = CreateImage("LargeModuleIcon", infoContent.transform, physicalDefense, false);
        RectTransform largeIconRect = largeIconObject.GetComponent<RectTransform>();
        largeIconRect.anchorMin = largeIconRect.anchorMax = new Vector2(0f, 0.5f);
        largeIconRect.pivot = new Vector2(0.5f, 0.5f);
        largeIconRect.anchoredPosition = new Vector2(175f, 0f);
        largeIconRect.sizeDelta = new Vector2(250f, 250f);

        GameObject textArea = CreateUIObject("TextArea", infoContent.transform);
        Stretch(textArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(335f, 50f), new Vector2(48f, 50f));

        TextMeshProUGUI nameText = CreateText(
            "ModuleNameText",
            textArea.transform,
            "물리 내성 증강",
            headingFont,
            56f,
            TextColor,
            TextAlignmentOptions.Left);
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = Vector2.zero;
        nameRect.sizeDelta = new Vector2(0f, 82f);
        nameText.fontStyle = FontStyles.Bold;

        GameObject divider = CreateImage("Divider", textArea.transform, null, false);
        RectTransform dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0f, 1f);
        dividerRect.anchorMax = new Vector2(1f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 1f);
        dividerRect.anchoredPosition = new Vector2(0f, -96f);
        dividerRect.sizeDelta = new Vector2(0f, 4f);
        divider.GetComponent<Image>().color = DividerColor;

        TextMeshProUGUI descriptionText = CreateText(
            "DescriptionText",
            textArea.transform,
            "장착 시, 플레이어의 물리 내성을 10 증가",
            bodyFont,
            34f,
            MutedTextColor,
            TextAlignmentOptions.TopLeft);
        RectTransform descriptionRect = descriptionText.rectTransform;
        descriptionRect.anchorMin = Vector2.zero;
        descriptionRect.anchorMax = Vector2.one;
        descriptionRect.offsetMin = new Vector2(0f, 0f);
        descriptionRect.offsetMax = new Vector2(0f, -118f);
        descriptionText.textWrappingMode = TextWrappingModes.Normal;

        GameObject inventoryArea = CreatePanel("ModuleInventoryArea", rightArea.transform, new Color(1f, 1f, 1f, 0.32f));
        Stretch(inventoryArea.GetComponent<RectTransform>(), new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.53f), Vector2.zero, Vector2.zero);

        TextMeshProUGUI inventoryLabel = CreateText(
            "InventoryLabel",
            inventoryArea.transform,
            "보유 모듈",
            headingFont,
            28f,
            MutedTextColor,
            TextAlignmentOptions.Left);
        RectTransform labelRect = inventoryLabel.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(28f, -12f);
        labelRect.sizeDelta = new Vector2(-56f, 46f);

        Transform[] rows = new Transform[3];
        int[] rowCounts = { 5, 4, 5 };
        float[] rowY = { 130f, 0f, -130f };
        int slotNumber = 1;
        ModuleSlotUI defaultSelection = null;

        for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            GameObject row = CreateUIObject($"Row{rowIndex + 1}", inventoryArea.transform, typeof(HorizontalLayoutGroup));
            rows[rowIndex] = row.transform;
            SetCenteredRect(row.GetComponent<RectTransform>(), new Vector2(850f, 155f), new Vector2(0f, rowY[rowIndex]));

            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = -10f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            for (int column = 0; column < rowCounts[rowIndex]; column++)
            {
                GameObject slot = InstantiateSlot(slotPrefab, row.transform, $"Slot{slotNumber:00}");
                slot.GetComponent<RectTransform>().sizeDelta = new Vector2(155f, 155f);

                Sprite icon = null;
                string title = string.Empty;
                string body = string.Empty;
                bool selectable = false;

                if (slotNumber == 1)
                {
                    icon = magicDefense;
                    title = "마법 내성 증강";
                    body = "장착 시, 플레이어의 마법 내성을 10 증가";
                    selectable = true;
                }
                else if (slotNumber == 2)
                {
                    icon = physicalDefense;
                    title = "물리 내성 증강";
                    body = "장착 시, 플레이어의 물리 내성을 10 증가";
                    selectable = true;
                }

                ModuleSlotUI slotUI = ConfigureSlot(slot, modulePage, icon, title, body, selectable);
                if (slotNumber == 2)
                    defaultSelection = slotUI;

                slotNumber++;
            }
        }

        modulePage.Configure(
            infoContent,
            largeIconObject.GetComponent<Image>(),
            nameText,
            descriptionText,
            defaultSelection,
            showInitialSelection: true);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (renderPreview)
            RenderCanvasPreview(canvas, PreviewPath);
    }

    private static GameObject BuildSlotPrefab(Sprite emptySlot)
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/UI");

        GameObject root = CreateUIObject("ModuleSlot", null, typeof(Image), typeof(Button), typeof(ModuleSlotUI));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(170f, 170f);

        Image frame = root.GetComponent<Image>();
        frame.sprite = emptySlot;
        frame.preserveAspect = true;
        frame.raycastTarget = true;

        Button button = root.GetComponent<Button>();
        button.targetGraphic = frame;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.82f);
        colors.pressedColor = new Color(0.78f, 0.86f, 1f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        GameObject iconObject = CreateImage("ModuleIcon", root.transform, null, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        Stretch(iconRect, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(3f, 3f));
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
        slotUI.Configure(page, iconImage, icon, title, body, selectable);
        EditorUtility.SetDirty(slotUI);
        return slotUI;
    }

    private static GameObject InstantiateSlot(GameObject prefab, Transform parent, string name)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = name;
        instance.layer = 5;
        return instance;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string text,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUIObject(name, parent, typeof(TextMeshProUGUI));
        TextMeshProUGUI textComponent = textObject.GetComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.font = font;
        textComponent.fontSize = fontSize;
        textComponent.color = color;
        textComponent.alignment = alignment;
        textComponent.raycastTarget = false;
        textComponent.overflowMode = TextOverflowModes.Ellipsis;
        return textComponent;
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panel = CreateUIObject(name, parent, typeof(Image));
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return panel;
    }

    private static GameObject CreateImage(string name, Transform parent, Sprite sprite, bool raycastTarget)
    {
        GameObject imageObject = CreateUIObject(name, parent, typeof(Image));
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = sprite != null;
        image.raycastTarget = raycastTarget;
        image.color = Color.white;
        return imageObject;
    }

    private static GameObject CreateUIObject(string name, Transform parent, params Type[] additionalComponents)
    {
        Type[] components = new[] { typeof(RectTransform) }
            .Concat(additionalComponents.Where(type => type != typeof(RectTransform)))
            .Distinct()
            .ToArray();

        GameObject gameObject = new(name, components);
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

    private static void SetCenteredRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
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

        RenderMode originalMode = canvas.renderMode;
        Camera originalCamera = canvas.worldCamera;
        float originalPlaneDistance = canvas.planeDistance;

        GameObject cameraObject = new("ModulePreviewCamera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.white;
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
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100f;
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
            canvas.renderMode = originalMode;
            canvas.worldCamera = originalCamera;
            canvas.planeDistance = originalPlaneDistance;
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(screenshot);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
}
