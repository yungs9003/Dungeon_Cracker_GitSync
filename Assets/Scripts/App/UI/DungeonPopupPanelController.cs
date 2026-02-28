using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Dungeon.Core;
using Dungeon.Manager;

namespace Dungeon.UI
{
public class DungeonPopupPanelController : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Preview")]
    [SerializeField] private Image previewImage;

    [Header("Texts")]
    [SerializeField] private TMP_Text dungeonNameText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Farmable Items (dynamic prefab)")]
    [SerializeField] private Transform farmItemContainer;   // FarmItemsContainer 연결
    [SerializeField] private GameObject farmItemPrefab;     // FarmItemIcon 프리팹 연결

    [Header("Clear Circles (3)")]
    [SerializeField] private Image[] clearCircles; // size 3
    [SerializeField] private Sprite circleEmpty;
    [SerializeField] private Sprite circleBlueFilled;
    [SerializeField] private Sprite circleWhiteFilled;

    private string currentDungeonId = "";
    private string targetSceneName = "";

    private void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;
        panelRoot.SetActive(false);
    }

    public void OpenDungeon(string dungeonId)
    {
        currentDungeonId = dungeonId;

        if (DungeonDatabase.Instance == null)
        {
            Debug.LogError("[DungeonPopup] DungeonDatabase.Instance is null. Add DungeonDatabase to scene.");
            return;
        }
        if (SaveManager.Instance == null)
        {
            Debug.LogError("[DungeonPopup] SaveManager.Instance is null. Add SaveManager to scene.");
            return;
        }

        if (!DungeonDatabase.Instance.TryGetDungeon(dungeonId, out var info))
        {
            Debug.LogError($"[DungeonPopup] Dungeon not found: {dungeonId}");
            return;
        }

        targetSceneName = info.sceneName;

        // --- Preview ---
        if (previewImage != null)
        {
            var sp = Resources.Load<Sprite>(info.previewSpritePath);
            if (sp == null)
                Debug.LogError($"[DungeonPopup] Preview sprite not found: '{info.previewSpritePath}'");

            previewImage.sprite = sp;
            previewImage.enabled = (sp != null);
            previewImage.preserveAspect = true;
        }

        // --- Texts ---
        if (dungeonNameText != null) dungeonNameText.text = info.name ?? "";
        if (descriptionText != null) descriptionText.text = info.description ?? "";

        // --- Farm Items (dynamic) ---
        BuildFarmItems(info.farmableItems);

        // --- Clear Circles from Save ---
        var progress = SaveManager.Instance.GetOrCreateDungeonProgress(dungeonId);
        ApplyClearCircles(progress);

        panelRoot.SetActive(true);
    }

    private void BuildFarmItems(System.Collections.Generic.List<FarmItemInfo> items)
    {
        if (farmItemContainer == null || farmItemPrefab == null) return;

        // 1) 이전에 생성된 아이콘 삭제
        for (int i = farmItemContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(farmItemContainer.GetChild(i).gameObject);
        }

        if (items == null) return;

        // 2) 아이템 개수만큼 프리팹 생성
        foreach (var item in items)
        {
            var go = Instantiate(farmItemPrefab, farmItemContainer);

            var img = go.GetComponent<Image>();
            if (img == null) img = go.GetComponentInChildren<Image>();

            if (img != null)
            {
                var sp = Resources.Load<Sprite>(item.iconSpritePath);
                if (sp == null)
                    Debug.LogError($"[DungeonPopup] Farm item icon not found: '{item.iconSpritePath}'");

                img.sprite = sp;
                img.enabled = (sp != null);
                img.preserveAspect = true;
            }
        }
    }

    private void ApplyClearCircles(DungeonProgress progress)
    {
        if (clearCircles == null || clearCircles.Length < 3) return;

        int achieved = 0;
        if (progress?.criteria != null)
        {
            for (int i = 0; i < progress.criteria.Length; i++)
                if (progress.criteria[i]) achieved++;
        }

        if (achieved >= 4)
        {
            for (int i = 0; i < 3; i++)
                SetCircle(clearCircles[i], circleWhiteFilled);
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                if (i < achieved) SetCircle(clearCircles[i], circleBlueFilled);
                else SetCircle(clearCircles[i], circleEmpty);
            }
        }
    }

    private void SetCircle(Image img, Sprite sp)
    {
        if (img == null) return;
        img.sprite = sp;
        img.enabled = (sp != null);
        img.preserveAspect = true;
    }

    public void Close()
    {
        panelRoot.SetActive(false);
    }

    public void StartDungeon()
    {
        panelRoot.SetActive(false);

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("[DungeonPopup] targetSceneName is empty.");
            return;
        }

        SceneManager.LoadScene(targetSceneName);
    }
}
}