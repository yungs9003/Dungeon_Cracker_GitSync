using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Dungeon.System
{
    [RequireComponent(typeof(Button))]
    public class LoadSceneByNameButton : MonoBehaviour
    {
        [Header("Scene Load")]
        [SerializeField] private SceneButtonLoader loader;
        [SerializeField] private string sceneName;

        [Header("Button")]
        [SerializeField] private Button button;

        public string SceneName => sceneName;
        public bool IsCurrentScene => IsTargetScene(SceneManager.GetActiveScene().name);

        private void Awake()
        {
            ResolveButton();
        }

        private void Start()
        {
            RefreshState();
        }

        /// <summary>
        /// 현재 Scene에 해당하는 버튼은 비활성화한다.
        /// </summary>
        public void RefreshState()
        {
            RefreshStateForScene(SceneManager.GetActiveScene().name);
        }

        public void RefreshStateForScene(string currentSceneName)
        {
            ResolveButton();

            if (button != null)
                button.interactable = !IsTargetScene(currentSceneName);
        }

        public bool IsTargetScene(string candidateSceneName)
        {
            return string.Equals(candidateSceneName, sceneName, global::System.StringComparison.Ordinal);
        }

        public void Configure(SceneButtonLoader sceneLoader, string targetSceneName, Button targetButton)
        {
            loader = sceneLoader;
            sceneName = targetSceneName;
            button = targetButton != null ? targetButton : GetComponent<Button>();
            RefreshState();
        }

        public void Go()
        {
            if (IsCurrentScene)
                return;

            if (loader != null)
                loader.LoadScene(sceneName);
            else
                SceneManager.LoadScene(sceneName);
        }

        private void ResolveButton()
        {
            if (button == null)
                button = GetComponent<Button>();
        }
    }
}
