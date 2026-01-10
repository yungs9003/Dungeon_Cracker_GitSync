using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PopupPanelController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Image previewImage;   // 팝업에 보여줄 이미지

    private int targetBuildIndex = -1;

    private void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;
        panelRoot.SetActive(false);
    }

    // ✅ 버튼이 호출: Resources 이미지 경로 + 씬 번호
    public void OpenWithSpriteAndBuildIndex(string resourceSpritePath, int buildIndex)
    {
        targetBuildIndex = buildIndex;

        // Resources에서 스프라이트 로드
        Sprite sp = Resources.Load<Sprite>(resourceSpritePath);
        if (sp == null)
        {
            Debug.LogError($"[Popup] Sprite not found in Resources: '{resourceSpritePath}'");
            // 이미지가 없더라도 팝업은 띄울지 말지는 선택
            // return; 
        }

        if (previewImage != null)
        {
            previewImage.sprite = sp;
            previewImage.enabled = (sp != null);

            // 선택: 이미지 비율 유지
            previewImage.preserveAspect = true;
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

        if (targetBuildIndex < 0)
        {
            Debug.LogError("[Popup] targetBuildIndex is not set.");
            return;
        }

        SceneManager.LoadScene(targetBuildIndex);
    }
}
