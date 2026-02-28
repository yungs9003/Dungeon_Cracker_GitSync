using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dungeon.UI
{
public class ButtonToNextScene : MonoBehaviour
{
    public void Go()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
}
}