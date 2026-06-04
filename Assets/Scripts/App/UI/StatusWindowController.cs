using UnityEngine;

namespace Dungeon.UI
{
    public class StatusWindowController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject statusWindowRoot;

        [Header("Pages")]
        [SerializeField] private StatusPageUI statusPageUI;

        [Header("Input")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

        [Header("Options")]
        [SerializeField] private bool refreshWhenOpened = true;

        private bool isOpen;

        private void Awake()
        {
            SetOpen(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                Toggle();
        }

        public void Toggle()
        {
            SetOpen(!isOpen);
        }

        public void Open()
        {
            SetOpen(true);
        }

        public void Close()
        {
            SetOpen(false);
        }

        private void SetOpen(bool open)
        {
            isOpen = open;

            if (statusWindowRoot != null)
                statusWindowRoot.SetActive(isOpen);

            if (isOpen && refreshWhenOpened && statusPageUI != null)
                statusPageUI.Refresh();
        }
    }
}