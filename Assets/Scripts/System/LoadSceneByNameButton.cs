using UnityEngine;

namespace Dungeon.System
{
public class LoadSceneByNameButton : MonoBehaviour
{
    [SerializeField] private SceneButtonLoader loader;
    [SerializeField] private string sceneName;

    public void Go()
    {
        loader.LoadScene(sceneName);
    }
}
}