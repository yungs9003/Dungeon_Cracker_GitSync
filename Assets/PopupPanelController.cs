using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PopupPanelController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Image previewImage;

    private string targetSceneName = "";

    private void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;
        panelRoot.SetActive(false);
    }

    public void OpenWithSpriteAndSceneName(string resourceSpritePath, string sceneName)
    {
        targetSceneName = sceneName;

        Sprite sp = Resources.Load<Sprite>(resourceSpritePath);
        if (sp == null)
            Debug.LogError($"[Popup] Sprite not found in Resources: '{resourceSpritePath}'");

        if (previewImage != null)
        {
            previewImage.sprite = sp;
            previewImage.enabled = (sp != null);
            previewImage.preserveAspect = true; // 필요 시
        }

        panelRoot.SetActive(true);
    }

    public void Close()
    {
        panelRoot.SetActive(false);
    }

    public void Confirm()
    {
        panelRoot.SetActive(false);

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("[Popup] targetSceneName is empty.");
            return;
        }

        SceneManager.LoadScene(targetSceneName);
    }
}
