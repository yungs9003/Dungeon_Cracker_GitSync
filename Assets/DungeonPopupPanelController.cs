using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class DungeonPopupPanelController : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Preview")]
    [SerializeField] private Image previewImage;

    [Header("Texts")]
    [SerializeField] private TMP_Text dungeonNameText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Farmable Item Slots (fixed)")]
    [SerializeField] private Image[] farmItemSlots;

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

        // --- Farm Items ---
        if (farmItemSlots != null)
        {
            for (int i = 0; i < farmItemSlots.Length; i++)
            {
                var slot = farmItemSlots[i];
                if (slot == null) continue;

                if (info.farmableItems != null && i < info.farmableItems.Count)
                {
                    var item = info.farmableItems[i];
                    var icon = Resources.Load<Sprite>(item.iconSpritePath);
                    if (icon == null)
                        Debug.LogError($"[DungeonPopup] Farm item icon not found: '{item.iconSpritePath}'");

                    slot.sprite = icon;
                    slot.enabled = (icon != null);
                    slot.preserveAspect = true;
                    slot.gameObject.SetActive(true);
                }
                else
                {
                    slot.sprite = null;
                    slot.enabled = false;
                    slot.gameObject.SetActive(false);
                }
            }
        }

        // --- Clear Circles from Save ---
        var progress = SaveManager.Instance.GetOrCreateDungeonProgress(dungeonId);
        ApplyClearCircles(progress);

        panelRoot.SetActive(true);
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
            // 4개 달성 => 3개 모두 하얀색 채움
            for (int i = 0; i < 3; i++)
                SetCircle(clearCircles[i], circleWhiteFilled);
        }
        else
        {
            // 0~3개 => achieved 개수만큼 파란색 채움, 나머지 빈 동그라미
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
