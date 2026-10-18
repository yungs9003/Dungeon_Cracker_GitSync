using System;
using System.Linq;
using Dungeon.Core;
using Dungeon.UI;
using Dungeon.UI.Guild;
using Dungeon.UI.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ShopSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Shop.unity";
    public const string PagePath = "Assets/Prefabs/UI/Shop/ShopPage.prefab";
    public const string CardPath = "Assets/Prefabs/UI/Shop/ShopItemCard.prefab";
    public const string RowPath = "Assets/Prefabs/UI/Shop/ShopReceiptRow.prefab";
    private const string Art = "Assets/Sprites/UI/Shop/";
    private static TMP_FontAsset Body => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Cafe24Danjunghae-v2.0 SDF.asset");
    private static TMP_FontAsset Heading => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Cafe24Dangdanghae-v2.0 SDF.asset");

    [MenuItem("Tools/Dungeon/Rebuild Shop Scene UI")]
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
        if (Body == null || Heading == null) throw new InvalidOperationException("Shop fonts missing.");
        var cards = BuildCard(); var rows = BuildReceipt(); var page = BuildPage(cards, rows);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var canvas = scene.GetRootGameObjects().First(root => root.name == "Canvas").GetComponent<Canvas>();
        foreach (string oldName in new[] { "ShopImage", "Image", "ShopPage" })
        { var old = canvas.transform.Find(oldName); if (old != null) Object.DestroyImmediate(old.gameObject); }
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(page, canvas.transform);
        instance.name = "ShopPage"; instance.transform.SetAsFirstSibling();
        if (!scene.GetRootGameObjects().Any(root => root.GetComponentInChildren<GameDatabase>(true) != null))
            new GameObject("ShopDatabase").AddComponent<GameDatabase>();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("[ShopSceneBuilder] Shop UI, DB items, receipt and savedata checkout configured.");
    }
    private static ShopItemCardUI BuildCard()
    {
        var root = UI("ShopItemCard", null); Rect(root, 0, 0, 192, 351);
        var hit = root.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
        Line(root.transform, new Vector2(96, 6), new Vector2(59, 65), 4, new Color(.5f, .5f, .5f));
        Line(root.transform, new Vector2(96, 6), new Vector2(133, 65), 4, new Color(.5f, .5f, .5f));
        Round("Medallion", root.transform, 7.5f, 54, 177, 177, new Color(.85f, .85f, .85f));
        Round("Pin", root.transform, 87, -3, 18, 18, new Color(.85f, .85f, .85f));
        var icon = Image("Icon", root.transform, null, 24, 72, 144, 144); icon.preserveAspect = true;
        Image("PriceCard", root.transform, null, 0, 252, 192, 94.5f).color = new Color(.85f, .85f, .85f);
        var name = Text("ItemName", root.transform, "아이템", Body, 24, 6, 256, 180, 31);
        name.enableAutoSizing = true; name.fontSizeMin = 18; name.fontSizeMax = 24;
        Image("Divider", root.transform, Sprite(Art + "Group 136.png"), 6, 283, 180, 3);
        var price = Text("Price", root.transform, "0G", Body, 48, 6, 285, 180, 61.5f); price.alignment = TextAlignmentOptions.MidlineRight;
        price.enableAutoSizing = true; price.fontSizeMin = 30; price.fontSizeMax = 48; price.overflowMode = TextOverflowModes.Overflow;
        var view = root.AddComponent<ShopItemCardUI>(); view.Configure(icon, name, price);
        var saved = PrefabUtility.SaveAsPrefabAsset(root, CardPath); Object.DestroyImmediate(root);
        return saved.GetComponent<ShopItemCardUI>();
    }
    private static ShopReceiptRowUI BuildReceipt()
    {
        var root = UI("ShopReceiptRow", null); Rect(root, 0, 0, 384, 126);
        var layout = root.AddComponent<LayoutElement>(); layout.preferredHeight = 126;
        var title = Text("CodeAndName", root.transform, "000 급성치료제", Body, 36, 0, 0, 384, 48);
        title.enableAutoSizing = true; title.fontSizeMin = 24; title.fontSizeMax = 36;
        var quantity = Text("Quantity", root.transform, "1", Body, 38, 168, 58, 78, 51); quantity.alignment = TextAlignmentOptions.Midline;
        var amount = Text("Amount", root.transform, "150G", Body, 38, 252, 58, 132, 51); amount.alignment = TextAlignmentOptions.MidlineRight;
        amount.enableAutoSizing = true; amount.fontSizeMin = 20; amount.fontSizeMax = 38;
        var view = root.AddComponent<ShopReceiptRowUI>(); view.Configure(title, quantity, amount);
        var saved = PrefabUtility.SaveAsPrefabAsset(root, RowPath); Object.DestroyImmediate(root);
        return saved.GetComponent<ShopReceiptRowUI>();
    }
    private static GameObject BuildPage(ShopItemCardUI cards, ShopReceiptRowUI rows)
    {
        var root = UI("ShopPage", null); var rect = root.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(1920, 1080);
        var background = root.AddComponent<Image>(); background.color = Color.black; background.raycastTarget = false;
        root.AddComponent<GuildReferenceLayout>();
        Image("Clerk", root.transform, Sprite(Art + "KakaoTalk_20261017_165017229.png"), 0, 0, 768, 948);
        Image("ShelfBackground", root.transform, Sprite(Art + "Rectangle 189.png"), 768, 0, 699, 948);
        Image("UpperDisplay", root.transform, Sprite(Art + "display_line_1.png"), 768, 0, 699, 357);
        Image("LowerDisplay", root.transform, Sprite(Art + "display_line_1.png"), 768, 405, 699, 357);
        var products = UI("Products", root.transform); Rect(products, 792, 45, 654, 756);
        var grid = products.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(192, 351); grid.spacing = new Vector2(39, 54);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
        var goldBox = Image("Wallet", root.transform, null, 792, 840, 648, 75); goldBox.color = Color.white;
        var goldTitle = Text("WalletTitle", goldBox.transform, "보유 골드", Body, 32, 18, 16, 210, 48);
        var gold = Text("OwnedGold", goldBox.transform, "— G", Body, 42, 228, 12, 396, 51); gold.alignment = TextAlignmentOptions.MidlineRight;
        Image("ReceiptPaper", root.transform, Sprite(Art + "descript_area (1).png"), 1467, 0, 453, 949.125f);
        Image("StoreLogo", root.transform, Sprite(Art + "Group 134.png"), 1569, 36, 258, 75);
        var receiptTitle = Text("ReceiptTitle", root.transform, "영수증", Body, 60, 1512, 126, 384, 78); receiptTitle.alignment = TextAlignmentOptions.Center;
        DashedLine(root.transform, 1512, 214.5f, 384);
        Text("ProductColumn", root.transform, "상품명", Body, 36, 1512, 228, 156, 48);
        var countTitle = Text("QuantityColumn", root.transform, "수량", Body, 36, 1680, 228, 78, 48); countTitle.alignment = TextAlignmentOptions.Center;
        var amountTitle = Text("AmountColumn", root.transform, "금액", Body, 36, 1764, 228, 132, 48); amountTitle.alignment = TextAlignmentOptions.MidlineRight;
        DashedLine(root.transform, 1512, 283.5f, 384);
        var scroll = Scroll(root.transform, out var receiptContent);
        DashedLine(root.transform, 1512, 681, 384);
        Text("TotalTitle", root.transform, "합계", Body, 60, 1512, 724.5f, 150, 75);
        var total = Text("TotalGold", root.transform, "0G", Body, 60, 1662, 724.5f, 234, 75); total.alignment = TextAlignmentOptions.MidlineRight;
        total.enableAutoSizing = true; total.fontSizeMin = 30; total.fontSizeMax = 60;
        var feedback = Text("Feedback", root.transform, string.Empty, Body, 24, 1512, 798, 384, 36); feedback.alignment = TextAlignmentOptions.Center;
        var payment = Round("Checkout", root.transform, 1563, 840, 261, 75, new Color(.55f, .55f, .55f)); payment.raycastTarget = true;
        var checkout = payment.gameObject.AddComponent<Button>(); checkout.targetGraphic = payment; checkout.transition = Selectable.Transition.None;
        checkout.interactable = false; DisableNavigation(checkout);
        var payLabel = Text("Label", payment.transform, "결제하기", Heading, 48, 0, 0, 261, 75); payLabel.color = Color.white; payLabel.alignment = TextAlignmentOptions.Center;
        var commentBox = Image("Comment", root.transform, Sprite("Assets/Sprites/UI/Guild/CommentBox.png"), 75.7413f, 630, 616.518f, 300);
        commentBox.raycastTarget = true; var next = commentBox.gameObject.AddComponent<Button>(); next.targetGraphic = commentBox; next.transition = Selectable.Transition.None; DisableNavigation(next);
        var comment = Text("CommentText", commentBox.transform, "그거 괜찮지?\n이번주 입고된 신상이야.", Heading, 54, 36, 25, 548, 223);
        comment.color = Color.white; comment.enableAutoSizing = true; comment.fontSizeMin = 30; comment.fontSizeMax = 54; comment.textWrappingMode = TextWrappingModes.Normal;
        Image("Next", commentBox.transform, Sprite("Assets/Sprites/UI/Guild/CommentNext.png"), 546, 253, 48, 32);
        var popupRoot = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/ItemEquipmentInfoPopup.prefab"), root.transform);
        popupRoot.name = "ShopItemInfoPopup";
        var page = root.AddComponent<ShopPageUI>(); page.Configure(cards, rows, products.GetComponent<RectTransform>(), receiptContent,
            scroll, gold, total, feedback, checkout, next, comment, popupRoot.GetComponent<ItemEquipmentInfoPopupUI>());
        var preview = new GameObject("ShopPreviewDatabase");
        try { var db = preview.AddComponent<GameDatabase>(); db.LoadDatabase(); page.PopulateProducts(db.Items); }
        finally { Object.DestroyImmediate(preview); }
        Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(products.GetComponent<RectTransform>());
        var saved = PrefabUtility.SaveAsPrefabAsset(root, PagePath); Object.DestroyImmediate(root); return saved;
    }
    private static ScrollRect Scroll(Transform parent, out RectTransform content)
    {
        var root = UI("ReceiptItems", parent); Rect(root, 1512, 309, 384, 351);
        var scroll = root.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.inertia = true; scroll.scrollSensitivity = 65;
        var viewport = UI("Viewport", root.transform); Stretch(viewport.GetComponent<RectTransform>());
        var hit = viewport.AddComponent<Image>(); hit.color = Color.clear; viewport.AddComponent<RectMask2D>();
        var holder = UI("Content", viewport.transform); content = holder.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
        var layout = holder.AddComponent<VerticalLayoutGroup>(); layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        holder.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.content = content; return scroll;
    }
    private static void DisableNavigation(Selectable item)
    { var navigation = item.navigation; navigation.mode = Navigation.Mode.None; item.navigation = navigation; }
    private static void DashedLine(Transform parent, float x, float y, float width)
    { for (int i = 0; i < 12; i++) Image("Dash", parent, null, x + i * width / 12, y, width / 12 - 12, 6).color = Color.black; }
    private static void Line(Transform parent, Vector2 from, Vector2 to, float width, Color color)
    {
        var image = Image("Cord", parent, null, 0, 0, 1, 1); image.color = color;
        var rect = image.rectTransform; rect.pivot = Vector2.one * .5f; Vector2 center = (from + to) * .5f;
        rect.anchoredPosition = new Vector2(center.x, -center.y); rect.sizeDelta = new Vector2(Vector2.Distance(from, to), width);
        rect.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
    }
    private static GameObject UI(string name, Transform parent)
    { var root = new GameObject(name, typeof(RectTransform)); root.layer = 5; if (parent != null) root.transform.SetParent(parent, false); return root; }
    private static void Rect(GameObject root, float x, float y, float width, float height)
    { var rect = root.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); }
    private static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static Sprite Sprite(string path)
    { return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault() ?? throw new InvalidOperationException("Missing Shop sprite: " + path); }
    private static Image Image(string name, Transform parent, Sprite sprite, float x, float y, float width, float height)
    { var root = UI(name, parent); Rect(root, x, y, width, height); var image = root.AddComponent<Image>(); image.sprite = sprite; image.raycastTarget = false; return image; }
    private static ShopRoundedGraphic Round(string name, Transform parent, float x, float y, float width, float height, Color color)
    { var root = UI(name, parent); Rect(root, x, y, width, height); var image = root.AddComponent<ShopRoundedGraphic>(); image.color = color; image.raycastTarget = false; return image; }
    private static TextMeshProUGUI Text(string name, Transform parent, string value, TMP_FontAsset font, float size, float x, float y, float width, float height)
    {
        var root = UI(name, parent); Rect(root, x, y, width, height); var text = root.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = size; text.text = value; text.color = Color.black; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.alignment = TextAlignmentOptions.TopLeft; text.overflowMode = TextOverflowModes.Ellipsis; return text;
    }
}
