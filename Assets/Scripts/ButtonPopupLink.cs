using UnityEngine;

public class ButtonPopupLink : MonoBehaviour
{
    [SerializeField] private PopupPanelController popup;

    [Header("Resources Sprite Path (without extension)")]
    [SerializeField] private string resourceSpritePath; // 예: "UI/ClearImage"

    [Header("Target Scene Name")]
    [SerializeField] private string targetSceneName; // 예: "MainMenu"

    public void OpenPopup()
    {
        if (popup == null)
        {
            Debug.LogError("[ButtonPopupLink] PopupPanelController is not assigned.");
            return;
        }

        popup.OpenWithSpriteAndSceneName(resourceSpritePath, targetSceneName);
    }
}
