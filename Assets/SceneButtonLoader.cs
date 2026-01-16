using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneButtonLoader : MonoBehaviour
{
    // 씬 이름으로 이동
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}
