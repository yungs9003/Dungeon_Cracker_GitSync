using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dungeon.System
{
    [RequireComponent(typeof(Button))]
    public class MenuButtonItem : MonoBehaviour,
        ISelectHandler,
        IPointerClickHandler
    {
        [Header("Menu")]
        [SerializeField] private MenuController menuController;

        [Header("Description")]
        [TextArea(2, 5)]
        [SerializeField] private string description;

        public string Description => description;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();

            if (menuController == null)
            {
                menuController =
                    GetComponentInParent<MenuController>();
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            menuController?.SelectItem(this);
        }

        public void OnPointerClick(
            PointerEventData eventData)
        {
            if (button == null || !button.interactable)
                return;

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(
                    gameObject
                );
            }

            menuController?.SelectItem(this);
        }
    }
}