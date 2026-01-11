using UnityEngine;
using UnityEngine.SceneManagement;

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    [Header("Scene Names")]
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private string worldMapScene = "WorldMap";

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void GoToMainMenu()
    {
        SceneManager.LoadScene(mainMenuScene, LoadSceneMode.Single);
    }

    public void GoToWorldMap()
    {
        SceneManager.LoadScene(worldMapScene, LoadSceneMode.Single);
    }

    public void QuitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        // 에디터에서는 Application.Quit이 안 먹어서 플레이 종료로 처리
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
