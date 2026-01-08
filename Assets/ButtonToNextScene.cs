using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonToNextScene : MonoBehaviour
{
    public void Go()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
}
