using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dungeon.System
{
    public class MenuController : MonoBehaviour
    {
        [Header("Menu Items")]
        [SerializeField] private List<MenuButtonItem> menuItems = new();

        [Header("Description")]
        [SerializeField] private TMP_Text descriptionText;

        private void Awake()
        {
            if (menuItems.Count == 0)
            {
                menuItems.AddRange(
                    GetComponentsInChildren<MenuButtonItem>(true)
                );
            }

            ConfigureNavigation();
        }

        private void OnEnable()
        {
            StartCoroutine(SelectFirstButton());
        }

        private IEnumerator SelectFirstButton()
        {
            yield return null;

            foreach (MenuButtonItem item in menuItems)
            {
                if (item == null)
                    continue;

                Button button = item.GetComponent<Button>();

                if (button == null || !button.interactable)
                    continue;

                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                    EventSystem.current.SetSelectedGameObject(
                        item.gameObject
                    );
                }

                SelectItem(item);
                break;
            }
        }

        public void SelectItem(MenuButtonItem item)
        {
            if (item == null || descriptionText == null)
                return;

            descriptionText.text = item.Description;
        }

        private void ConfigureNavigation()
        {
            List<Button> buttons = new();

            foreach (MenuButtonItem item in menuItems)
            {
                if (item == null)
                    continue;

                Button button = item.GetComponent<Button>();

                if (button != null)
                    buttons.Add(button);
            }

            if (buttons.Count == 0)
                return;

            for (int i = 0; i < buttons.Count; i++)
            {
                Navigation navigation = buttons[i].navigation;
                navigation.mode = Navigation.Mode.Explicit;

                navigation.selectOnUp =
                    buttons[(i - 1 + buttons.Count) % buttons.Count];

                navigation.selectOnDown =
                    buttons[(i + 1) % buttons.Count];

                buttons[i].navigation = navigation;
            }
        }
    }
}