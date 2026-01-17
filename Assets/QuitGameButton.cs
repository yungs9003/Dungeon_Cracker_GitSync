using UnityEngine;

public class QuitGameButton : MonoBehaviour
{
    public void Quit()
    {
#if UNITY_EDITOR
        // 에디터에서는 종료 대신 플레이 모드 종료
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
