using UnityEngine;

public class ButtonPopupLink : MonoBehaviour
{
    [SerializeField] private PopupPanelController popup;

    [Header("Resources Sprite Path (without extension)")]
    [SerializeField] private string resourceSpritePath; 

    [Header("Target Scene Build Index")]
    [SerializeField] private int targetBuildIndex = 0;

    public void OpenPopup()
    {
        if (popup == null)
        {
            Debug.LogError("[ButtonPopupLink] PopupPanelController is not assigned.");
            return;
        }

        popup.OpenWithSpriteAndBuildIndex(resourceSpritePath, targetBuildIndex);
    }
}
