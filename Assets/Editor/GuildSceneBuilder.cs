using System;
using System.Linq;
using Dungeon.UI.Guild;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>제공된 Guild.svg의 640×360 좌표를 1920×1080 UGUI로 구성합니다.</summary>
public static class GuildSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Guild.unity";
    public const string PagePath = "Assets/Prefabs/UI/Guild/GuildPage.prefab";
    public const string CatalogPath = "Assets/Resources/UI/Guild/GuildCatalog.asset";
    private const string Art = "Assets/Sprites/UI/Guild/";
    private const string CasePath = "Assets/Prefabs/UI/Guild/GuildDungeonCase.prefab";
    private const string RowPath = "Assets/Prefabs/UI/Guild/GuildAchievementRow.prefab";
    private static readonly Color Red = new(.69f, 0f, 0f, 1f);
    private static TMP_FontAsset Body => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Cafe24Danjunghae-v2.0 SDF.asset");
    private static TMP_FontAsset Heading => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Cafe24Dangdanghae-v2.0 SDF.asset");
    private static TMP_FontAsset English => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Koulen-Regular SDF.asset");

    [MenuItem("Tools/Dungeon/Rebuild Guild Scene UI")]
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
        if (Body == null || Heading == null || English == null) throw new InvalidOperationException("Guild UI fonts missing.");
        var catalog = LoadOrCreateCatalog();
        var cases = BuildCasePrefab();
        var rows = BuildRowPrefab();
        var pagePrefab = BuildPagePrefab(catalog, cases, rows);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var canvas = scene.GetRootGameObjects().First(root => root.name == "Canvas").GetComponent<Canvas>();
        foreach (string oldName in new[] { "GuildImage", "Image", "GuildPage" })
        {
            var old = canvas.transform.Find(oldName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = .5f;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(pagePrefab, canvas.transform);
        instance.name = "GuildPage"; instance.transform.SetAsFirstSibling();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("[GuildSceneBuilder] Guild page built; existing common bottom navigation and scene services preserved.");
    }

    private static GuildCatalog LoadOrCreateCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GuildCatalog>(CatalogPath);
        if (catalog != null) return catalog;
        catalog = ScriptableObject.CreateInstance<GuildCatalog>();
        catalog.comments = new[]
        {
            "항상 고생이 많으세요~\n쿠키라도 하나\n어떠신가요?",
            "완수한 도전과제가 있나요?\n케이스를 골라\n확인해 주세요!",
            "다음 모험도\n응원하고 있을게요!"
        };
        catalog.initialDungeonId = "DUNGEON_002";
        catalog.dungeons = Enumerable.Range(1, 5).Select(number => new GuildDungeonDefinition
        {
            id = $"DUNGEON_{number:000}", caseNumber = number,
            dungeonName = number == 2 ? "서울숲" : $"던전 {number:00}",
            achievements = new[]
            {
                new GuildAchievementDefinition { id = "clear_within_30_seconds", condition = "30초 안에 던전 답파", rewardLabel = "600G",
                    previewState = number == 2 ? GuildAchievementState.Certified : GuildAchievementState.Locked },
                new GuildAchievementDefinition { id = "clear_within_10_hits", condition = "피격 횟수 10회 이내로 던전 답파", rewardLabel = "600G",
                    previewState = number == 2 ? GuildAchievementState.Verifiable : GuildAchievementState.Locked }
            }
        }).ToArray();
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        return catalog;
    }

    private static GuildDungeonCaseUI BuildCasePrefab()
    {
        var root = UI("GuildDungeonCase", null); Rect(root, 0, 0, 348f, 210f);
        var image = root.AddComponent<Image>(); image.sprite = Sprite("CaseNormal");
        var button = root.AddComponent<Button>(); button.targetGraphic = image; ConfigureButton(button);
        var layout = root.AddComponent<LayoutElement>(); layout.preferredHeight = 210f; layout.preferredWidth = 348f;
        var number = Text("CaseNumber", root.transform, "No.01", Body, 96f, new Color(.831f, .596f, .235f), 86, 34, 262, 140);
        number.alignment = TextAlignmentOptions.MidlineLeft;
        var view = root.AddComponent<GuildDungeonCaseUI>();
        view.ConfigureArtwork(image, number, Sprite("CaseNormal"), Sprite("CaseSelected"));
        var saved = PrefabUtility.SaveAsPrefabAsset(root, CasePath); Object.DestroyImmediate(root);
        return saved.GetComponent<GuildDungeonCaseUI>();
    }

    private static GuildAchievementRowUI BuildRowPrefab()
    {
        var root = UI("GuildAchievementRow", null); Rect(root, 0, 0, 728, 253.5f);
        var layout = root.AddComponent<LayoutElement>(); layout.preferredHeight = 253.5f;
        var condition = Text("Condition", root.transform, "1) 30초 안에 던전 답파", Body, 46f, Color.black, 0, 0, 670, 94);
        condition.enableAutoSizing = true; condition.fontSizeMin = 32f; condition.fontSizeMax = 46f;
        condition.textWrappingMode = TextWrappingModes.Normal; condition.overflowMode = TextOverflowModes.Ellipsis;
        var reward = Text("Reward", root.transform, "REWARD : 600G", English, 40f, Red, 55, 133, 430, 60);
        var verifyObject = UI("Verify", root.transform); Rect(verifyObject, 537, 122, 133, 82);
        var hitArea = verifyObject.AddComponent<Image>(); hitArea.color = Color.clear;
        var button = verifyObject.AddComponent<Button>(); button.targetGraphic = hitArea; ConfigureButton(button);
        var label = Text("Label", verifyObject.transform, "VERIFY", English, 48f, Color.black, 0, 0, 133, 65);
        label.alignment = TextAlignmentOptions.Center;
        var underline = Image("Underline", verifyObject.transform, null, 11, 63, 123, 6); underline.color = Color.black;
        var divider = Image("Divider", root.transform, null, 0, 230, 670, 6); divider.color = Color.black;
        var stamp = Image("Certified", root.transform, Sprite("Certified"), 430, 28, 268, 193);
        var view = root.AddComponent<GuildAchievementRowUI>();
        view.ConfigureArtwork(condition, reward, button, label, underline, stamp.gameObject);
        view.SetState(GuildAchievementState.Locked);
        var saved = PrefabUtility.SaveAsPrefabAsset(root, RowPath); Object.DestroyImmediate(root);
        return saved.GetComponent<GuildAchievementRowUI>();
    }

    private static GameObject BuildPagePrefab(GuildCatalog catalog, GuildDungeonCaseUI cases, GuildAchievementRowUI rows)
    {
        var root = UI("GuildPage", null); var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(1920f, 1080f);
        var background = root.AddComponent<Image>(); background.color = Color.black; background.raycastTarget = false;
        root.AddComponent<GuildReferenceLayout>();
        Image("Clerk", root.transform, Sprite("GuildClerk"), 0, 0, 768.912f, 949.125f);
        var caseScroll = Scroll("DungeonCases", root.transform, 768.912f, 0, 348, 949.125f, out var caseContent);
        var paper = Image("DescriptionPanel", root.transform, Sprite("DescriptionPanel"), 1116.912f, 0, 803.088f, 949.125f);
        paper.color = Color.white;
        var caseLabel = Text("CaseLabel", root.transform, "CASE NO.02", English, 44, Red, 1192.176f, 37, 670, 62);
        var dungeonName = Text("DungeonName", root.transform, "서울숲", Heading, 108, Color.black, 1192.176f, 99, 670, 126);
        dungeonName.enableAutoSizing = true; dungeonName.fontSizeMin = 48; dungeonName.fontSizeMax = 108;
        dungeonName.overflowMode = TextOverflowModes.Ellipsis;
        Image("HeaderLine1", root.transform, null, 1192.176f, 230.8245f, 669.756f, 6.1754f).color = Color.black;
        Image("HeaderLine2", root.transform, null, 1192.176f, 246.6624f, 669.756f, 6.1754f).color = Color.black;
        var rowScroll = Scroll("Achievements", root.transform, 1192.176f, 279, 727.824f, 670.125f, out var rowContent);
        var box = Image("Comment", root.transform, Sprite("CommentBox"), 75.7413f, 630, 616.518f, 300);
        box.raycastTarget = true; var next = box.gameObject.AddComponent<Button>(); next.targetGraphic = box; ConfigureButton(next);
        var comment = Text("CommentText", box.transform, catalog.comments[0], Heading, 54f, Color.white, 36, 25, 548, 223);
        comment.enableAutoSizing = true; comment.fontSizeMin = 30f; comment.fontSizeMax = 54f;
        comment.textWrappingMode = TextWrappingModes.Normal;
        Image("Next", box.transform, Sprite("CommentNext"), 546, 253, 48, 32);
        var page = root.AddComponent<GuildPageUI>();
        page.Configure(catalog, cases, rows, caseContent, rowContent, rowScroll, next, comment, caseLabel, dungeonName);
        page.Initialize();
        Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(caseContent); LayoutRebuilder.ForceRebuildLayoutImmediate(rowContent);
        var saved = PrefabUtility.SaveAsPrefabAsset(root, PagePath); Object.DestroyImmediate(root);
        return saved;
    }

    private static ScrollRect Scroll(string name, Transform parent, float x, float y, float width, float height, out RectTransform content)
    {
        var root = UI(name, parent); Rect(root, x, y, width, height);
        var scroll = root.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.inertia = true; scroll.decelerationRate = .135f; scroll.scrollSensitivity = 65f;
        var viewport = UI("Viewport", root.transform); Stretch(viewport.GetComponent<RectTransform>());
        var hitArea = viewport.AddComponent<Image>(); hitArea.color = Color.clear;
        viewport.AddComponent<RectMask2D>();
        var holder = UI("Content", viewport.transform); content = holder.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1);
        content.sizeDelta = Vector2.zero; content.anchoredPosition = Vector2.zero;
        var layout = holder.AddComponent<VerticalLayoutGroup>(); layout.spacing = 0; layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        var fitter = holder.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.content = content;
        return scroll;
    }

    private static void ConfigureButton(Button button)
    {
        var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white; colors.pressedColor = new Color(.86f, .86f, .86f);
        colors.disabledColor = Color.white; button.colors = colors;
        var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
    }

    private static GameObject UI(string name, Transform parent)
    { var obj = new GameObject(name, typeof(RectTransform)); obj.layer = 5; if (parent != null) obj.transform.SetParent(parent, false); return obj; }

    private static void Rect(GameObject obj, float x, float y, float width, float height)
    {
        var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }

    private static Sprite Sprite(string name)
    { return AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png") ?? throw new InvalidOperationException("Missing Guild art: " + name); }

    private static Image Image(string name, Transform parent, Sprite sprite, float x, float y, float width, float height)
    { var obj = UI(name, parent); Rect(obj, x, y, width, height); var image = obj.AddComponent<Image>(); image.sprite = sprite; image.raycastTarget = false; return image; }

    private static TextMeshProUGUI Text(string name, Transform parent, string value, TMP_FontAsset font, float size, Color color, float x, float y, float width, float height)
    {
        var obj = UI(name, parent); Rect(obj, x, y, width, height); var text = obj.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = size; text.text = value; text.color = color; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.alignment = TextAlignmentOptions.TopLeft; text.margin = Vector4.zero;
        return text;
    }
}
