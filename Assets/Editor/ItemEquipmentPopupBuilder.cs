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

public static class ItemEquipmentPopupBuilder
{
    private const string AssetRoot = "Assets/Sprites/UI/Window/ItemEquipmentPopup";
    private const string PrefabPath = "Assets/Prefabs/UI/ItemEquipmentInfoPopup.prefab";
    private const string BackgroundPath = AssetRoot + "/ItemEquipmentPopup_Background.png";
    private const string ConsumablePath = AssetRoot + "/ItemType_Consumable.png";
    private const string MaterialPath = AssetRoot + "/ItemType_Material.png";
    private const string KeyPath = AssetRoot + "/ItemType_Key.png";
    private const string HeadPath = AssetRoot + "/EquipmentSlot_Head.png";
    private const string BodyPath = AssetRoot + "/EquipmentSlot_Body.png";
    private const string LegsPath = AssetRoot + "/EquipmentSlot_Legs.png";
    private const string KoulenFontPath = "Assets/Fonts/Koulen-Regular SDF.asset";
    private const string NameFontPath = "Assets/Fonts/Cafe24Dangdanghae-v2.0 SDF.asset";
    private const string BodyFontPath = "Assets/Fonts/Cafe24Danjunghae-v2.0 SDF.asset";

    private static readonly string[] RankPaths =
    {
        AssetRoot + "/Rank_0.png",
        AssetRoot + "/Rank_1.png",
        AssetRoot + "/Rank_2.png",
        AssetRoot + "/Rank_3.png",
        AssetRoot + "/Rank_4.png"
    };

    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/DUNGEON_001.unity",
        "Assets/Scenes/CheckpointScene.unity"
    };

    [MenuItem("Tools/Dungeon/Rebuild Item Equipment Popup")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            BuildAll();
            Debug.Log("[ItemEquipmentPopupBuilder] Popup prefab and scene instances rebuilt.");
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
            BuildAll();
            Debug.Log("[ItemEquipmentPopupBuilder] Command-line build completed.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void BuildAll()
    {
        ConfigureImporters();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        GameObject prefab = BuildPrefab();

        foreach (string scenePath in ScenePaths)
            InstallPrefabInScene(scenePath, prefab);

        AssetDatabase.SaveAssets();
    }

    private static GameObject BuildPrefab()
    {
        Sprite background = LoadRequired<Sprite>(BackgroundPath);
        Sprite consumable = LoadRequired<Sprite>(ConsumablePath);
        Sprite material = LoadRequired<Sprite>(MaterialPath);
        Sprite key = LoadRequired<Sprite>(KeyPath);
        Sprite head = LoadRequired<Sprite>(HeadPath);
        Sprite body = LoadRequired<Sprite>(BodyPath);
        Sprite legs = LoadRequired<Sprite>(LegsPath);
        Sprite[] ranks = RankPaths
            .Select(path => LoadRequired<Sprite>(path))
            .ToArray();
        TMP_FontAsset koulen = LoadRequired<TMP_FontAsset>(KoulenFontPath);
        TMP_FontAsset nameFont = LoadRequired<TMP_FontAsset>(NameFontPath);
        TMP_FontAsset bodyFont = LoadRequired<TMP_FontAsset>(BodyFontPath);

        GameObject root = CreateUIObject(
            "ItemEquipmentInfoPopup",
            null,
            typeof(ItemEquipmentInfoPopupUI));
        Stretch(root.GetComponent<RectTransform>());

        GameObject content = CreateUIObject("Content", root.transform);
        Stretch(content.GetComponent<RectTransform>());

        GameObject dismissObject = CreateUIObject(
            "DismissArea",
            content.transform,
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        Stretch(dismissObject.GetComponent<RectTransform>());
        Image dismissImage = dismissObject.GetComponent<Image>();
        dismissImage.color = new Color(0f, 0f, 0f, 0f);
        dismissImage.raycastTarget = true;
        Button dismissButton = dismissObject.GetComponent<Button>();
        dismissButton.transition = Selectable.Transition.None;
        dismissButton.targetGraphic = dismissImage;

        GameObject panel = CreateUIObject(
            "PopupPanel",
            content.transform,
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(360f, 378f);
        Image panelImage = panel.GetComponent<Image>();
        panelImage.sprite = background;
        panelImage.type = Image.Type.Simple;
        panelImage.preserveAspect = false;
        panelImage.color = Color.white;
        panelImage.raycastTarget = true;

        TextMeshProUGUI category = CreateText(
            "CategoryText",
            panel.transform,
            "-EQUIPMENT-",
            koulen,
            24f,
            Color.white,
            TextAlignmentOptions.Center,
            false);
        SetTopLeftRect(category.rectTransform, new Vector2(360f, 45f), Vector2.zero);
        category.characterSpacing = 8f;

        Image icon = CreateImage("ItemIcon", panel.transform, null);
        SetTopLeftRect(icon.rectTransform, new Vector2(78f, 78f), new Vector2(45f, 78f));
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TextMeshProUGUI itemName = CreateText(
            "NameText",
            panel.transform,
            "고글 투구",
            nameFont,
            39f,
            HexColor("6CACFF"),
            TextAlignmentOptions.Center,
            false);
        SetTopLeftRect(itemName.rectTransform, new Vector2(195f, 58f), new Vector2(142f, 53f));
        itemName.overflowMode = TextOverflowModes.Ellipsis;

        Image typeTag = CreateImage("TypeTag", panel.transform, head);
        SetTopLeftRect(typeTag.rectTransform, new Vector2(63f, 27f), new Vector2(159f, 108f));

        Image rankTag = CreateImage("RankTag", panel.transform, ranks[0]);
        SetTopLeftRect(rankTag.rectTransform, new Vector2(84f, 27f), new Vector2(159f, 144f));

        Image divider = CreateImage("Divider", panel.transform, null);
        SetTopLeftRect(divider.rectTransform, new Vector2(294f, 3f), new Vector2(33f, 175.5f));
        divider.color = HexColor("D9D9D9");
        divider.raycastTarget = false;

        TextMeshProUGUI description = CreateText(
            "DescriptionText",
            panel.transform,
            "최대체력 +50",
            bodyFont,
            24f,
            Color.black,
            TextAlignmentOptions.TopLeft,
            true);
        SetTopLeftRect(
            description.rectTransform,
            new Vector2(294f, 165f),
            new Vector2(33f, 195f));
        description.overflowMode = TextOverflowModes.Ellipsis;
        description.lineSpacing = 4f;

        ItemEquipmentInfoPopupUI controller =
            root.GetComponent<ItemEquipmentInfoPopupUI>();
        controller.Configure(
            null,
            content,
            dismissButton,
            icon,
            category,
            itemName,
            description,
            typeTag,
            rankTag,
            consumable,
            material,
            key,
            head,
            body,
            legs,
            ranks);

        content.SetActive(false);

        EnsureFolder(Path.GetDirectoryName(PrefabPath)?.Replace('\\', '/'));
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);

        if (prefab == null)
            throw new InvalidOperationException($"Failed to create popup prefab: {PrefabPath}");

        return prefab;
    }

    private static void InstallPrefabInScene(string scenePath, GameObject prefab)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject statusRoot = scene.GetRootGameObjects()
            .FirstOrDefault(root => root.name == "Status");

        if (statusRoot == null)
            throw new InvalidOperationException($"Status root was not found: {scenePath}");

        Transform popupLayer = statusRoot.transform.Find(
            "StatusWindowRoot/StatusPanel/PopupLayer");

        if (popupLayer == null)
            throw new InvalidOperationException($"PopupLayer was not found: {scenePath}");

        popupLayer.gameObject.SetActive(true);
        Stretch((RectTransform)popupLayer);

        GameObject instance = null;

        for (int index = popupLayer.childCount - 1; index >= 0; index--)
        {
            Transform child = popupLayer.GetChild(index);

            if (child.name == "EquipmentPopup")
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
                continue;
            }

            bool isPopup =
                child.name == "ItemEquipmentInfoPopup" ||
                child.GetComponent<ItemEquipmentInfoPopupUI>() != null;

            if (!isPopup)
                continue;

            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(
                child.gameObject
            );

            if (instance == null && source == prefab)
            {
                instance = child.gameObject;
                continue;
            }

            UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        if (instance == null)
            instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;

        if (instance == null)
            throw new InvalidOperationException($"Failed to instantiate popup: {scenePath}");

        instance.name = "ItemEquipmentInfoPopup";
        instance.transform.SetParent(popupLayer, false);
        Stretch(instance.GetComponent<RectTransform>());
        instance.transform.SetAsLastSibling();

        if (instance.GetComponent<ItemEquipmentInfoPopupUI>() == null)
            throw new InvalidOperationException($"Popup controller is missing: {scenePath}");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureImporters()
    {
        ConfigureSpriteImporter(BackgroundPath, FilterMode.Bilinear);
        ConfigureSpriteImporter(ConsumablePath, FilterMode.Point);
        ConfigureSpriteImporter(MaterialPath, FilterMode.Point);
        ConfigureSpriteImporter(KeyPath, FilterMode.Point);
        ConfigureSpriteImporter(HeadPath, FilterMode.Point);
        ConfigureSpriteImporter(BodyPath, FilterMode.Point);
        ConfigureSpriteImporter(LegsPath, FilterMode.Point);

        foreach (string rankPath in RankPaths)
            ConfigureSpriteImporter(rankPath, FilterMode.Point);
    }

    private static void ConfigureSpriteImporter(string assetPath, FilterMode filterMode)
    {
        if (!File.Exists(assetPath))
            throw new FileNotFoundException("Popup sprite was not found.", assetPath);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (importer == null)
            throw new InvalidOperationException($"TextureImporter was not found: {assetPath}");

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = filterMode;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }

    private static T LoadRequired<T>(string assetPath) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

        if (asset == null)
            throw new InvalidOperationException($"Required asset was not found: {assetPath}");

        return asset;
    }

    private static GameObject CreateUIObject(
        string name,
        Transform parent,
        params Type[] componentTypes)
    {
        Type[] types = new Type[componentTypes.Length + 1];
        types[0] = typeof(RectTransform);
        Array.Copy(componentTypes, 0, types, 1, componentTypes.Length);
        GameObject gameObject = new GameObject(name, types);
        gameObject.layer = LayerMask.NameToLayer("UI");

        if (parent != null)
            gameObject.transform.SetParent(parent, false);

        return gameObject;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite)
    {
        GameObject gameObject = CreateUIObject(
            name,
            parent,
            typeof(CanvasRenderer),
            typeof(Image));
        Image image = gameObject.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = sprite != null;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string value,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment,
        bool wrap)
    {
        GameObject gameObject = CreateUIObject(
            name,
            parent,
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Normal;
        text.color = color;
        text.alignment = alignment;
        text.text = value;
        text.textWrappingMode = wrap
            ? TextWrappingModes.Normal
            : TextWrappingModes.NoWrap;
        text.richText = true;
        text.raycastTarget = false;
        return text;
    }

    private static void SetTopLeftRect(
        RectTransform rect,
        Vector2 size,
        Vector2 topLeft)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static Color HexColor(string html)
    {
        if (!ColorUtility.TryParseHtmlString("#" + html, out Color color))
            throw new InvalidOperationException($"Invalid color: {html}");

        return color;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string leaf = Path.GetFileName(folderPath);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
