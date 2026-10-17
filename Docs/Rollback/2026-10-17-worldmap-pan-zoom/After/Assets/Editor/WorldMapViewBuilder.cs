using System;
using System.Linq;
using Dungeon.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class WorldMapViewBuilder
{
    public const string ScenePath = "Assets/Scenes/WorldMap.unity";
    public const string ImagePath = "Assets/Sprites/UI/WorldMap/a52b2a42-eb89-404f-b3b8-ae165dae228b (1).png";

    [MenuItem("Tools/Dungeon/Configure WorldMap Pan and Zoom")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try { Build(); }
        finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
    }

    public static void Build()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(ImagePath);
        if (importer == null) throw new InvalidOperationException("WorldMap image missing.");
        if (importer.maxTextureSize < 4096) { importer.maxTextureSize = 4096; importer.SaveAndReimport(); }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ImagePath);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var canvas = scene.GetRootGameObjects().First(root => root.name == "Canvas").GetComponent<Canvas>();
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = 1f;
        var navigation = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
            .FirstOrDefault(rect => rect.name == "BottomPanel");
        if (navigation == null) throw new InvalidOperationException("Common bottom navigation panel missing.");
        var window = canvas.transform.Find("WorldMapViewport") as RectTransform;
        if (window == null)
        {
            window = UI("WorldMapViewport", canvas.transform);
            window.gameObject.AddComponent<Image>().color = new Color(.075f, .38f, .51f, 1f);
            window.gameObject.AddComponent<RectMask2D>();
        }
        window.SetAsFirstSibling(); window.anchorMin = Vector2.zero; window.anchorMax = Vector2.one;
        window.pivot = new Vector2(.5f, .5f); window.offsetMin = window.offsetMax = Vector2.zero;
        var content = (canvas.transform.Find("BG") ?? window.Find("MapContent")) as RectTransform;
        if (content == null) throw new InvalidOperationException("Existing WorldMap BG missing.");
        content.SetParent(window, false); content.name = "MapContent";
        var image = content.GetComponent<Image>(); image.sprite = sprite; image.preserveAspect = false; image.raycastTarget = true;
        var buttons = (canvas.transform.Find("Buttons") ?? content.Find("Buttons")) as RectTransform;
        if (buttons == null) throw new InvalidOperationException("Existing WorldMap button container missing.");
        buttons.SetParent(content, false); buttons.anchorMin = Vector2.zero; buttons.anchorMax = Vector2.one;
        buttons.offsetMin = buttons.offsetMax = Vector2.zero; buttons.localScale = Vector3.one;
        var view = window.GetComponent<WorldMapPanZoomUI>() ?? window.gameObject.AddComponent<WorldMapPanZoomUI>();
        view.Configure(window, content, image, navigation);
        foreach (var button in buttons.GetComponentsInChildren<Button>(true))
        {
            var target = button.GetComponent<WorldMapFocusTargetUI>() ?? button.gameObject.AddComponent<WorldMapFocusTargetUI>();
            target.Configure(view);
            var legacy = button.GetComponent<ButtonPopupLink>();
            if (legacy != null)
            {
                bool moved = false;
                for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                    if (button.onClick.GetPersistentTarget(i) == legacy && button.onClick.GetPersistentMethodName(i) == "OpenPopup")
                    { UnityEventTools.RemovePersistentListener(button.onClick, i); moved = true; }
                if (moved && !HasListener(target.OnFocused, legacy, "OpenPopup")) UnityEventTools.AddPersistentListener(target.OnFocused, legacy.OpenPopup);
            }
            if (!HasListener(button.onClick, target, "Focus")) UnityEventTools.AddPersistentListener(button.onClick, target.Focus);
            EditorUtility.SetDirty(button); EditorUtility.SetDirty(target);
        }
        if (window.Find("ResetMapView") == null)
        {
            var reset = UI("ResetMapView", window);
            reset.anchorMin = reset.anchorMax = reset.pivot = Vector2.one;
            reset.anchoredPosition = new Vector2(-24f, -24f); reset.sizeDelta = new Vector2(148f, 46f);
            var background = reset.gameObject.AddComponent<Image>(); background.color = new Color(.04f, .13f, .18f, .9f);
            var button = reset.gameObject.AddComponent<Button>(); button.targetGraphic = background;
            UnityEventTools.AddPersistentListener(button.onClick, view.ResetView);
            var label = UI("Label", reset); label.anchorMin = Vector2.zero; label.anchorMax = Vector2.one; label.offsetMin = label.offsetMax = Vector2.zero;
            var text = label.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Cafe24Danjunghae-v2.0 SDF.asset");
            text.text = "원래 크기"; text.fontSize = 23f; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("[WorldMapViewBuilder] Native-size map, drag viewport and focus-before-popup linked.");
    }
    private static bool HasListener(UnityEvent value, UnityEngine.Object target, string method) =>
        Enumerable.Range(0, value.GetPersistentEventCount()).Any(i => value.GetPersistentTarget(i) == target && value.GetPersistentMethodName(i) == method);
    private static RectTransform UI(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.layer = 5; obj.transform.SetParent(parent, false);
        return obj.GetComponent<RectTransform>();
    }
}
