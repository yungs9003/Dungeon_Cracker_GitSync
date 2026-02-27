using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dungeon.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class WorldMapPausePopup : MonoBehaviour
{
    [Header("Popup Root (PausePopupRoot)")]
    [SerializeField] private GameObject popupRoot;

    [Header("Optional: freeze time while open")]
    [SerializeField] private bool freezeTime = false;

    private bool isOpen;
    private float prevTimeScale = 1f;

    private void Start()
    {
        CloseInstant();
    }

    private void Update()
    {
        if (EscapePressed())
        {
            if (isOpen) Close();
            else Open();
        }
    }

    private bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

    public void Open()
    {
        if (popupRoot == null) return;

        isOpen = true;
        popupRoot.SetActive(true);

        // 항상 최상단에 오도록
        popupRoot.transform.SetAsLastSibling();

        if (freezeTime)
        {
            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
    }

    public void Close()
    {
        if (popupRoot == null) return;

        isOpen = false;
        popupRoot.SetActive(false);

        if (freezeTime)
        {
            Time.timeScale = prevTimeScale;
        }
    }

    private void CloseInstant()
    {
        if (popupRoot == null) return;
        isOpen = false;
        popupRoot.SetActive(false);
    }

    // 버튼: "그만두기" (월드맵에 그대로 남기 = 팝업 닫기)
    public void OnClickCancel()
    {
        Close();
    }

    // 버튼: "메인메뉴로 돌아가기" (씬 전환)
    public void OnClickBackToMainMenu()
    {
        // 너가 전에 만든 GameFlowManager가 있다면 그걸 쓰는 게 제일 깔끔
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.GoToMainMenu();
        else
            SceneManager.LoadScene("MainMenu"); // 예비용(씬 이름 맞게)
    }
}
