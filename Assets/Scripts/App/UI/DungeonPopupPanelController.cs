using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Dungeon.Core;
using Dungeon.Manager;
using UnityEngine.Events;
using System;
using System.IO;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Dungeon.UI
{
public class DungeonPopupPanelController : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;
    [Header("Events")]
    [SerializeField] private UnityEvent onClosed = new();
    public UnityEvent OnClosed => onClosed;

    [Header("Reference Popup UI")]
    [SerializeField] private DungeonInfoPopupUI referenceView;
    [SerializeField] private Button outsideCloseButton;
    private static DungeonPopupPanelController activePopup;
    private static int lastClosedFrame = -1;
    public static bool IsAnyOpen => activePopup != null && activePopup.IsOpen;
    public static bool BlocksBackgroundInput => IsAnyOpen || lastClosedFrame == Time.frameCount;
    public bool IsOpen => panelRoot != null && panelRoot.activeInHierarchy;
    public string TargetSceneName => targetSceneName;

    [Header("Preview")]
    [SerializeField] private Image previewImage;

    [Header("Texts")]
    [SerializeField] private TMP_Text dungeonNameText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Farmable Items (dynamic prefab)")]
    [SerializeField] private Transform farmItemContainer;
    [SerializeField] private GameObject farmItemPrefab;

    [Header("Clear Circles (3)")]
    [SerializeField] private Image[] clearCircles;
    [SerializeField] private Sprite circleEmpty;
    [SerializeField] private Sprite circleBlueFilled;
    [SerializeField] private Sprite circleWhiteFilled;

    private string currentDungeonId = "";
    private string targetSceneName = "";

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        BindReferenceButtons();
        panelRoot.SetActive(false);
    }

    public void ConfigureReferenceView(DungeonInfoPopupUI view, Button outside)
    {
        UnbindReferenceButtons(); referenceView = view; outsideCloseButton = outside;
        if (Application.isPlaying) BindReferenceButtons();
    }
    private void BindReferenceButtons()
    {
        if (referenceView != null) referenceView.EnterButton.onClick.AddListener(StartDungeon);
        if (outsideCloseButton != null) outsideCloseButton.onClick.AddListener(Close);
    }
    private void UnbindReferenceButtons()
    {
        if (referenceView != null) referenceView.EnterButton.onClick.RemoveListener(StartDungeon);
        if (outsideCloseButton != null) outsideCloseButton.onClick.RemoveListener(Close);
    }
    private void OnDestroy()
    { UnbindReferenceButtons(); if (activePopup == this) activePopup = null; }
    private void Update()
    {
        if (referenceView == null || !IsOpen || Tutorial.TutorialOverlayUI.IsAnyOpen) return;
#if ENABLE_INPUT_SYSTEM
        bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        bool escape = Input.GetKeyDown(KeyCode.Escape);
#else
        bool escape = false;
#endif
        if (escape) Close();
    }

    public void OpenDungeon(string dungeonId)
    {
        currentDungeonId = dungeonId;

        if (DungeonDatabase.Instance == null)
        {
            Debug.LogError("[DungeonPopup] DungeonDatabase.Instance is null. Add DungeonDatabase to scene.");
            return;
        }

        if (!DungeonDatabase.Instance.TryGetDungeon(dungeonId, out var info))
        {
            Debug.LogError($"[DungeonPopup] Dungeon not found: {dungeonId}");
            return;
        }

        targetSceneName = info.sceneName;

        if (referenceView != null)
        {
            bool canEnter = TryResolveBuildScene(info.sceneName, out targetSceneName);
            referenceView.Present(info.id, info.name, canEnter);
            RectTransform anchor = null;
            foreach (var map in FindObjectsByType<WorldMapPanZoomUI>(FindObjectsSortMode.None))
                if (map.gameObject.scene == gameObject.scene && map.FocusedPoint != null)
                { anchor = map.FocusedPoint; break; }
            referenceView.GetComponent<DungeonInfoPopupLayout>()?.SetAnchor(anchor);
            panelRoot.SetActive(true); panelRoot.transform.SetAsLastSibling(); activePopup = this;
            return;
        }

        ApplyPreview(info);
        ApplyTexts(info);
        BuildFarmItems(info.farmableItems);

        int achieved = GetAchievedClearCount(dungeonId);
        ApplyClearCircles(achieved);

        panelRoot.SetActive(true);
        activePopup = this;
    }

    private static bool TryResolveBuildScene(string requested, out string resolved)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string name = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
            if (!string.IsNullOrWhiteSpace(requested) && string.Equals(name, requested, StringComparison.OrdinalIgnoreCase))
            { resolved = name; return true; }
        }
        resolved = ""; return false;
    }

    private void ApplyPreview(DungeonInfo info)
    {
        if (previewImage == null)
            return;

        var sp = Resources.Load<Sprite>(info.previewSpritePath);

        if (sp == null)
            Debug.LogError($"[DungeonPopup] Preview sprite not found: '{info.previewSpritePath}'");

        previewImage.sprite = sp;
        previewImage.enabled = sp != null;
        previewImage.preserveAspect = true;
    }

    private void ApplyTexts(DungeonInfo info)
    {
        if (dungeonNameText != null)
            dungeonNameText.text = info.name ?? "";

        if (descriptionText != null)
            descriptionText.text = info.description ?? "";
    }

    private void BuildFarmItems(List<FarmItemInfo> items)
    {
        if (farmItemContainer == null || farmItemPrefab == null)
            return;

        for (int i = farmItemContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(farmItemContainer.GetChild(i).gameObject);
        }

        if (items == null)
            return;

        foreach (var item in items)
        {
            var go = Instantiate(farmItemPrefab, farmItemContainer);

            var img = go.GetComponent<Image>();
            if (img == null)
                img = go.GetComponentInChildren<Image>();

            if (img == null)
                continue;

            var sp = Resources.Load<Sprite>(item.iconSpritePath);

            if (sp == null)
                Debug.LogError($"[DungeonPopup] Farm item icon not found: '{item.iconSpritePath}'");

            img.sprite = sp;
            img.enabled = sp != null;
            img.preserveAspect = true;
        }
    }

    private int GetAchievedClearCount(string dungeonId)
    {
        StatusSaveFile saveFile = StatusSaveFile.Instance;

        if (saveFile == null || saveFile.Data == null)
        {
            Debug.LogWarning("[DungeonPopup] StatusSaveFile.Instance is missing. Clear circles will be empty.");
            return 0;
        }

        var dungeonStatus = saveFile.Data.dungeon_status;

        if (dungeonStatus == null)
            return 0;

        foreach (var entry in dungeonStatus)
        {
            if (entry == null)
                continue;

            foreach (var pair in entry)
            {
                if (!IsSameDungeonKey(pair.Key, dungeonId))
                    continue;

                return ParseAchievedClearCount(pair.Value);
            }
        }

        return 0;
    }

    private bool IsSameDungeonKey(string savedKey, string requestedDungeonId)
    {
        if (savedKey == requestedDungeonId)
            return true;

        string savedDigits = ExtractDigits(savedKey);
        string requestedDigits = ExtractDigits(requestedDungeonId);

        return !string.IsNullOrEmpty(savedDigits)
               && !string.IsNullOrEmpty(requestedDigits)
               && savedDigits == requestedDigits;
    }

    private string ExtractDigits(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        string result = "";

        foreach (char c in text)
        {
            if (char.IsDigit(c))
                result += c;
        }

        return result;
    }

    private int ParseAchievedClearCount(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        value = value.Trim();

        if (int.TryParse(value, out int numericCount))
            return Mathf.Clamp(numericCount, 0, 4);

        int count = 0;

        string[] tokens = value.Split(':', ',', '/', '|', ';', ' ');

        foreach (string rawToken in tokens)
        {
            string token = rawToken.Trim().ToLower();

            if (string.IsNullOrEmpty(token))
                continue;

            if (token == "1" ||
                token == "true" ||
                token == "clear" ||
                token == "cleared" ||
                token == "yes")
            {
                count++;
            }
        }

        return Mathf.Clamp(count, 0, 4);
    }

    private void ApplyClearCircles(int achieved)
    {
        if (clearCircles == null || clearCircles.Length < 3)
            return;

        achieved = Mathf.Clamp(achieved, 0, 4);

        if (achieved >= 4)
        {
            for (int i = 0; i < 3; i++)
                SetCircle(clearCircles[i], circleWhiteFilled);
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                if (i < achieved)
                    SetCircle(clearCircles[i], circleBlueFilled);
                else
                    SetCircle(clearCircles[i], circleEmpty);
            }
        }
    }

    private void SetCircle(Image img, Sprite sp)
    {
        if (img == null)
            return;

        img.sprite = sp;
        img.enabled = sp != null;
        img.preserveAspect = true;
    }

    public void Close()
    {
        if (Tutorial.TutorialOverlayUI.IsAnyOpen) return;
        bool wasOpen = panelRoot.activeSelf;
        panelRoot.SetActive(false);
        if (wasOpen) { lastClosedFrame = Time.frameCount; onClosed.Invoke(); }
    }

    public void StartDungeon()
    {
        if (Tutorial.TutorialOverlayUI.IsAnyOpen) return;

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("[DungeonPopup] targetSceneName is empty.");
            return;
        }

        panelRoot.SetActive(false);
        SceneManager.LoadScene(targetSceneName);
    }
}
}
