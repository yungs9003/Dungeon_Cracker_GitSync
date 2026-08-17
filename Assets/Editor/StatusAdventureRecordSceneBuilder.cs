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

public static class StatusAdventureRecordSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/DUNGEON_001.unity";
    private const string FrameSpritePath = "Assets/Sprites/UI/Window/Frame 3 (2).png";
    private const string SRankSpritePath = "Assets/Sprites/UI/Window/AdventureRecordRankS.png";
    private const string ARankSpritePath = "Assets/Sprites/UI/Window/A_RANK_0.png";
    private const string BRankSpritePath = "Assets/Sprites/UI/Window/B_RANK_0.png";
    private const string CRankSpritePath = "Assets/Sprites/UI/Window/C_RANK_0.png";
    private const string FontPath = "Assets/Fonts/Koulen-Regular SDF.asset";

    private const int ReferenceWidth = 1920;
    private const int ReferenceHeight = 1080;
    private const int WindowWidth = 1584;
    private const int WindowHeight = 888;

    [MenuItem("Tools/Dungeon/Rebuild Status Adventure Record")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            Build(renderPreviews: false);
            Debug.Log("[StatusAdventureRecordSceneBuilder] Status Adventure Record rebuilt.");
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
            Build(renderPreviews: true);
            Debug.Log("[StatusAdventureRecordSceneBuilder] Build and previews completed.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Build(bool renderPreviews)
    {
        ConfigureSpriteImporter(FrameSpritePath);
        ConfigureSpriteImporter(SRankSpritePath);
        ConfigureSpriteImporter(ARankSpritePath);
        ConfigureSpriteImporter(BRankSpritePath);
        ConfigureSpriteImporter(CRankSpritePath);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Sprite frameSprite = LoadSprite(FrameSpritePath);
        Sprite sRankSprite = LoadSprite(SRankSpritePath);
        Sprite aRankSprite = LoadSprite(ARankSpritePath);
        Sprite bRankSprite = LoadSprite(BRankSpritePath);
        Sprite cRankSprite = LoadSprite(CRankSpritePath);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        EnsureAsset(frameSprite, FrameSpritePath);
        EnsureAsset(sRankSprite, SRankSpritePath);
        EnsureAsset(aRankSprite, ARankSpritePath);
        EnsureAsset(bRankSprite, BRankSpritePath);
        EnsureAsset(cRankSprite, CRankSpritePath);
        EnsureAsset(font, FontPath);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SceneObjects objects = ResolveSceneObjects(scene);
        BuildRecordPage(
            objects,
            frameSprite,
            sRankSprite,
            aRankSprite,
            bRankSprite,
            cRankSprite,
            font);
        RebuildOverlayControls(objects);
        ConfigureController(objects);
        Validate(objects);

        objects.TabSwitcher.SelectStatus();
        objects.Controller.ShowMainPage();
        objects.RecordPage.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        if (renderPreviews)
        {
            RenderPreview(objects, false, "Status_AdventureRecord_Main.png");
            RenderPreview(objects, true, "Status_AdventureRecord_Page.png");
        }
    }

    private static SceneObjects ResolveSceneObjects(Scene scene)
    {
        GameObject statusRoot = scene.GetRootGameObjects()
            .FirstOrDefault(root => root.name == "Status");

        if (statusRoot == null)
            throw new InvalidOperationException("DUNGEON_001/Status root was not found.");

        Transform windowRoot = Require(statusRoot.transform, "StatusWindowRoot");
        Transform clipMask = Require(windowRoot, "StatusPanel/StatusClipMask");
        Transform rightPanel = Require(clipMask, "RightPanel");
        Transform statusPage = Require(rightPanel, "ContentPanel/StatusPage");
        Transform itemPage = Require(rightPanel, "ContentPanel/ItemPage");
        Transform overlayRoot = clipMask.Find("OverlayControls");

        Transform statusArrow = overlayRoot != null
            ? overlayRoot.Find("StatusPage/BottomRightArrowButton")
            : null;
        Transform itemArrow = overlayRoot != null
            ? overlayRoot.Find("ItemPage/BottomRightArrowButton")
            : null;

        statusArrow ??= statusPage.Find("BottomRightArrowButton");
        itemArrow ??= itemPage.Find("BottomRightArrowButton");

        if (statusArrow == null)
            throw new InvalidOperationException("Status page BottomRightArrowButton was not found.");
        if (itemArrow == null)
            throw new InvalidOperationException("Item page BottomRightArrowButton was not found.");

        StatusTabSwitcher tabSwitcher = windowRoot.GetComponent<StatusTabSwitcher>();

        if (tabSwitcher == null)
            throw new InvalidOperationException("StatusWindowRoot/StatusTabSwitcher was not found.");

        Canvas canvas = statusRoot.GetComponent<Canvas>();

        if (canvas == null)
            throw new InvalidOperationException("Status root Canvas was not found.");

        return new SceneObjects
        {
            Scene = scene,
            StatusRoot = statusRoot,
            WindowRoot = windowRoot.gameObject,
            Canvas = canvas,
            ClipMask = (RectTransform)clipMask,
            MainBackground = Require(clipMask, "MainBackground"),
            LeftPanel = Require(clipMask, "LeftPanel"),
            RightPanel = rightPanel,
            BottomBar = Require(clipMask, "BottomBar"),
            TopBar = Require(clipMask, "TopBar"),
            StatusPage = statusPage.gameObject,
            StatusArrow = statusArrow,
            ItemArrow = itemArrow,
            TabSwitcher = tabSwitcher
        };
    }

    private static void BuildRecordPage(
        SceneObjects objects,
        Sprite frameSprite,
        Sprite sRankSprite,
        Sprite aRankSprite,
        Sprite bRankSprite,
        Sprite cRankSprite,
        TMP_FontAsset font)
    {
        Transform oldPage = objects.ClipMask.Find("AdventureRecordPage");

        if (oldPage != null)
            UnityEngine.Object.DestroyImmediate(oldPage.gameObject);

        GameObject page = CreateUIObject(
            "AdventureRecordPage",
            objects.ClipMask,
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform pageRect = page.GetComponent<RectTransform>();
        Stretch(pageRect);

        Image frame = page.GetComponent<Image>();
        frame.sprite = frameSprite;
        frame.type = Image.Type.Simple;
        frame.preserveAspect = false;
        frame.color = Color.white;
        frame.raycastTarget = true;

        GameObject dynamicContent = CreateUIObject(
            "DynamicContent",
            page.transform,
            typeof(AdventureRecordDataView));
        Stretch(dynamicContent.GetComponent<RectTransform>());

        TextMeshProUGUI playTime = CreateValueText(
            "PlayTimeValue", dynamicContent.transform, "135 : 26", font, 48f);
        SetTopLeftRect(
            playTime.rectTransform,
            new Vector2(600f, 70f),
            new Vector2(273f, 319.406f));

        TextMeshProUGUI clearedDungeon = CreateValueText(
            "ClearedDungeonValue",
            dynamicContent.transform,
            "<color=#6CACFF>28</color> / <color=#6C88AF>73</color> / 96",
            font,
            48f);
        SetTopLeftRect(
            clearedDungeon.rectTransform,
            new Vector2(600f, 70f),
            new Vector2(273f, 436.406f));

        TextMeshProUGUI adventureCount = CreateValueText(
            "AdventureCountValue", dynamicContent.transform, "128 TIMES", font, 48f);
        SetTopLeftRect(
            adventureCount.rectTransform,
            new Vector2(600f, 70f),
            new Vector2(273f, 553.406f));

        TextMeshProUGUI achievement = CreateValueText(
            "GuildAchievementValue",
            dynamicContent.transform,
            "<color=#6CACFF>87</color> / 300",
            font,
            48f);
        SetTopLeftRect(
            achievement.rectTransform,
            new Vector2(600f, 70f),
            new Vector2(273f, 676.406f));

        GameObject rankImageObject = CreateUIObject(
            "RankImage",
            dynamicContent.transform,
            typeof(CanvasRenderer),
            typeof(Image));
        SetTopLeftRect(
            rankImageObject.GetComponent<RectTransform>(),
            new Vector2(453f, 453f),
            new Vector2(873f, 339f));
        Image rankImage = rankImageObject.GetComponent<Image>();
        rankImage.sprite = sRankSprite;
        rankImage.type = Image.Type.Simple;
        rankImage.preserveAspect = true;
        rankImage.raycastTarget = false;
        rankImage.color = Color.white;

        TextMeshProUGUI rankFallback = CreateValueText(
            "RankFallbackText", dynamicContent.transform, "S", font, 320f);
        SetTopLeftRect(
            rankFallback.rectTransform,
            new Vector2(453f, 453f),
            new Vector2(873f, 339f));
        rankFallback.color = new Color32(108, 172, 255, 255);
        rankFallback.gameObject.SetActive(false);

        AdventureRecordDataView dataView =
            dynamicContent.GetComponent<AdventureRecordDataView>();
        dataView.Configure(
            playTime,
            clearedDungeon,
            adventureCount,
            achievement,
            rankImage,
            rankFallback,
            sRankSprite,
            aRankSprite,
            bRankSprite,
            cRankSprite);
        ValidateRankMapping(dataView, rankImage, rankFallback, "S", sRankSprite);
        ValidateRankMapping(dataView, rankImage, rankFallback, "A", aRankSprite);
        ValidateRankMapping(dataView, rankImage, rankFallback, "B", bRankSprite);
        ValidateRankMapping(dataView, rankImage, rankFallback, "C", cRankSprite);

        dataView.ApplyData(new StatusSaveData { player_rank = "D" });

        if (rankImage.gameObject.activeSelf || !rankFallback.gameObject.activeSelf)
            throw new InvalidOperationException("Unknown rank fallback validation failed.");

        dataView.ApplyData(new StatusSaveData
        {
            play_time = "135:26",
            clear_num_info = "28:73:96",
            adventure_count = 128,
            achievement = "87:300",
            player_rank = "s"
        });

        page.SetActive(false);
        objects.RecordPage = page;
        objects.DataView = dataView;
    }

    private static void ValidateRankMapping(
        AdventureRecordDataView dataView,
        Image rankImage,
        TMP_Text rankFallback,
        string rankKey,
        Sprite expectedSprite)
    {
        dataView.ApplyData(new StatusSaveData { player_rank = rankKey });

        if (!rankImage.gameObject.activeSelf || rankImage.sprite != expectedSprite)
        {
            throw new InvalidOperationException(
                $"{rankKey} rank sprite mapping validation failed.");
        }

        if (rankFallback.gameObject.activeSelf)
            throw new InvalidOperationException($"{rankKey} rank fallback must be hidden.");
    }

    private static void RebuildOverlayControls(SceneObjects objects)
    {
        Bounds statusArrowBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            objects.ClipMask,
            objects.StatusArrow);
        Bounds itemArrowBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            objects.ClipMask,
            objects.ItemArrow);

        Transform oldOverlay = objects.ClipMask.Find("OverlayControls");

        if (oldOverlay != null)
        {
            objects.StatusArrow.SetParent(objects.ClipMask, true);
            objects.ItemArrow.SetParent(objects.ClipMask, true);
            UnityEngine.Object.DestroyImmediate(oldOverlay.gameObject);
        }

        GameObject overlay = CreateUIObject("OverlayControls", objects.ClipMask);
        Stretch(overlay.GetComponent<RectTransform>());

        GameObject statusGroup = CreateUIObject("StatusPage", overlay.transform);
        Stretch(statusGroup.GetComponent<RectTransform>());
        GameObject itemGroup = CreateUIObject("ItemPage", overlay.transform);
        Stretch(itemGroup.GetComponent<RectTransform>());

        ReparentAtPreservedBounds(
            objects.StatusArrow,
            (RectTransform)statusGroup.transform,
            objects.ClipMask,
            statusArrowBounds);
        ReparentAtPreservedBounds(
            objects.ItemArrow,
            (RectTransform)itemGroup.transform,
            objects.ClipMask,
            itemArrowBounds);

        statusGroup.SetActive(true);
        itemGroup.SetActive(false);
        objects.OverlayControls = overlay.transform;
        objects.StatusArrowGroup = statusGroup;
        objects.ItemArrowGroup = itemGroup;

        objects.MainBackground.SetSiblingIndex(0);
        objects.LeftPanel.SetSiblingIndex(1);
        objects.RightPanel.SetSiblingIndex(2);
        objects.RecordPage.transform.SetSiblingIndex(3);
        objects.BottomBar.SetSiblingIndex(4);
        objects.TopBar.SetSiblingIndex(5);
        objects.OverlayControls.SetSiblingIndex(6);
    }

    private static void ConfigureController(SceneObjects objects)
    {
        StatusAdventureRecordPageUI controller =
            objects.ClipMask.GetComponent<StatusAdventureRecordPageUI>();

        if (controller == null)
            controller = objects.ClipMask.gameObject.AddComponent<StatusAdventureRecordPageUI>();

        controller.Configure(
            objects.TabSwitcher,
            objects.StatusPage,
            objects.StatusArrow.GetComponent<Button>(),
            objects.RecordPage,
            objects.ItemArrowGroup,
            0,
            3,
            true);
        objects.Controller = controller;
        EditorUtility.SetDirty(controller);
    }

    private static void Validate(SceneObjects objects)
    {
        if (objects.ClipMask.childCount != 7)
            throw new InvalidOperationException(
                $"StatusClipMask must have 7 direct children, found {objects.ClipMask.childCount}.");

        string[] expectedOrder =
        {
            "MainBackground",
            "LeftPanel",
            "RightPanel",
            "AdventureRecordPage",
            "BottomBar",
            "TopBar",
            "OverlayControls"
        };

        for (int i = 0; i < expectedOrder.Length; i++)
        {
            if (objects.ClipMask.GetChild(i).name != expectedOrder[i])
            {
                throw new InvalidOperationException(
                    $"StatusClipMask child {i} must be {expectedOrder[i]}, " +
                    $"found {objects.ClipMask.GetChild(i).name}.");
            }
        }

        Image frame = objects.RecordPage.GetComponent<Image>();

        if (frame == null || frame.sprite == null || !frame.raycastTarget)
            throw new InvalidOperationException("AdventureRecordPage frame/raycast is not configured.");
        if (objects.StatusArrow.parent.name != "StatusPage")
            throw new InvalidOperationException("Status arrow is not inside OverlayControls/StatusPage.");
        if (objects.ItemArrow.parent.name != "ItemPage")
            throw new InvalidOperationException("Item arrow is not inside OverlayControls/ItemPage.");
        if (objects.ItemArrow.GetComponent<PageFlipUI>() == null)
            throw new InvalidOperationException("The existing Item PageFlipUI was not preserved.");
        if (objects.DataView == null)
            throw new InvalidOperationException("AdventureRecordDataView was not configured.");

        objects.TabSwitcher.SelectStatus();
        objects.Controller.ShowAdventureRecord();

        if (!objects.RecordPage.activeSelf || !objects.StatusArrow.gameObject.activeInHierarchy)
            throw new InvalidOperationException("Status record page/arrow toggle validation failed.");

        objects.Controller.ShowMainPage();

        if (objects.RecordPage.activeSelf)
            throw new InvalidOperationException("Status main page restore validation failed.");

        objects.TabSwitcher.SelectItem();
        objects.Controller.ShowMainPage();

        if (!objects.ItemArrowGroup.activeSelf || objects.StatusArrow.gameObject.activeSelf)
            throw new InvalidOperationException("Item overlay arrow validation failed.");

        objects.TabSwitcher.SelectStatus();
        objects.Controller.ShowMainPage();
        Debug.Log("[StatusAdventureRecordSceneBuilder] Hierarchy, layers, data, and toggles validated.");
    }

    private static void RenderPreview(SceneObjects objects, bool showRecord, string fileName)
    {
        GameObject[] roots = objects.Scene.GetRootGameObjects();
        bool[] rootStates = roots.Select(root => root.activeSelf).ToArray();
        bool windowState = objects.WindowRoot.activeSelf;
        RenderMode canvasMode = objects.Canvas.renderMode;
        Camera canvasCamera = objects.Canvas.worldCamera;
        float planeDistance = objects.Canvas.planeDistance;

        GameObject cameraObject = new("StatusAdventureRecordPreviewCamera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(38, 43, 52, 255);
        camera.orthographic = true;
        camera.orthographicSize = ReferenceHeight * 0.5f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 2000f;
        camera.transform.position = new Vector3(0f, 0f, -1000f);
        camera.cullingMask = 1 << 5;

        RenderTexture target = new(
            ReferenceWidth,
            ReferenceHeight,
            24,
            RenderTextureFormat.ARGB32);
        Texture2D screenshot = new(
            WindowWidth,
            WindowHeight,
            TextureFormat.RGBA32,
            false);

        try
        {
            for (int i = 0; i < roots.Length; i++)
                roots[i].SetActive(roots[i] == objects.StatusRoot);

            objects.StatusRoot.SetActive(true);
            objects.WindowRoot.SetActive(true);
            objects.TabSwitcher.SelectStatus();

            if (showRecord)
                objects.Controller.ShowAdventureRecord();
            else
                objects.Controller.ShowMainPage();

            objects.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            objects.Canvas.worldCamera = camera;
            objects.Canvas.planeDistance = 100f;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();

            camera.Render();
            RenderTexture.active = target;
            screenshot.ReadPixels(
                new Rect(
                    (ReferenceWidth - WindowWidth) * 0.5f,
                    (ReferenceHeight - WindowHeight) * 0.5f,
                    WindowWidth,
                    WindowHeight),
                0,
                0);
            screenshot.Apply();

            string outputPath = Path.GetFullPath(fileName);
            File.WriteAllBytes(outputPath, screenshot.EncodeToPNG());
            Debug.Log($"[StatusAdventureRecordSceneBuilder] Preview saved: {outputPath}");
        }
        finally
        {
            RenderTexture.active = null;
            camera.targetTexture = null;
            objects.Canvas.renderMode = canvasMode;
            objects.Canvas.worldCamera = canvasCamera;
            objects.Canvas.planeDistance = planeDistance;
            objects.Controller.ShowMainPage();
            objects.WindowRoot.SetActive(windowState);

            for (int i = 0; i < roots.Length; i++)
                roots[i].SetActive(rootStates[i]);

            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(screenshot);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    private static TextMeshProUGUI CreateValueText(
        string objectName,
        Transform parent,
        string value,
        TMP_FontAsset font,
        float fontSize)
    {
        GameObject textObject = CreateUIObject(
            objectName,
            parent,
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Normal;
        text.color = Color.black;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.richText = true;
        text.characterSpacing = 9.375f;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private static void ReparentAtPreservedBounds(
        Transform child,
        RectTransform newParent,
        RectTransform reference,
        Bounds oldBounds)
    {
        RectTransform childRect = (RectTransform)child;
        Vector2 size = oldBounds.size;
        Vector2 center = oldBounds.center;

        child.SetParent(newParent, false);
        childRect.anchorMin = childRect.anchorMax = new Vector2(1f, 0f);
        childRect.pivot = new Vector2(0.5f, 0.5f);
        childRect.sizeDelta = size;
        childRect.anchoredPosition = new Vector2(
            center.x - reference.rect.xMax,
            center.y - reference.rect.yMin);
        childRect.localRotation = Quaternion.identity;
        childRect.localScale = Vector3.one;
    }

    private static GameObject CreateUIObject(
        string objectName,
        Transform parent,
        params Type[] additionalComponents)
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

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetTopLeftRect(RectTransform rect, Vector2 size, Vector2 topLeft)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static Transform Require(Transform root, string path)
    {
        Transform result = root.Find(path);

        if (result == null)
            throw new InvalidOperationException($"Required Status hierarchy path was not found: {path}");

        return result;
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }

    private static void EnsureAsset(UnityEngine.Object asset, string path)
    {
        if (asset == null)
            throw new FileNotFoundException($"Required asset was not found: {path}", path);
    }

    private static void ConfigureSpriteImporter(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer == null)
            throw new FileNotFoundException($"Sprite source was not found: {path}", path);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        TextureImporterSettings importerSettings = new();
        importer.ReadTextureSettings(importerSettings);
        importerSettings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(importerSettings);
        importer.SaveAndReimport();
    }

    private sealed class SceneObjects
    {
        public Scene Scene;
        public GameObject StatusRoot;
        public GameObject WindowRoot;
        public Canvas Canvas;
        public RectTransform ClipMask;
        public Transform MainBackground;
        public Transform LeftPanel;
        public Transform RightPanel;
        public Transform BottomBar;
        public Transform TopBar;
        public GameObject StatusPage;
        public Transform StatusArrow;
        public Transform ItemArrow;
        public StatusTabSwitcher TabSwitcher;
        public GameObject RecordPage;
        public AdventureRecordDataView DataView;
        public Transform OverlayControls;
        public GameObject StatusArrowGroup;
        public GameObject ItemArrowGroup;
        public StatusAdventureRecordPageUI Controller;
    }
}
