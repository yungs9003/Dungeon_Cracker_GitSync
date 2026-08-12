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

        private void Awake()
        {
            // Inspector에서 직접 넣지 않았으면
            // 같은 오브젝트에 붙어있는 Button을 자동으로 가져옴
            if (button == null)
                button = GetComponent<Button>();
        }

        private void Start()
        {
            UpdateButtonState();
        }

        /// <summary>
        /// 현재 Scene에 해당하는 버튼은 비활성화한다.
        /// </summary>
        private void UpdateButtonState()
        {
            string currentSceneName = SceneManager.GetActiveScene().name;

            bool isCurrentScene = currentSceneName == sceneName;

            button.interactable = !isCurrentScene;
        }

        public void Go()
        {
            // 현재 Scene 버튼은 interactable = false라
            // 일반적으로 호출될 일이 없지만 한 번 더 방지
            if (SceneManager.GetActiveScene().name == sceneName)
                return;

            loader.LoadScene(sceneName);
        }
    }
}