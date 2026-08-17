using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Dungeon.Core;

namespace Dungeon.UI
{
    /// <summary>
    /// Displays one non-consumable item. Single click opens the shared info popup.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ExpendableSlotUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button button;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text typeText;

        private SlotPointerClickRelay pointerClickRelay;
        private UnityAction singleLeftClickAction;

        public string CurrentKey { get; private set; } = GameDatabase.NoneKey;
        public ItemDefinition CurrentItem { get; private set; }

        private void Awake()
        {
            AutoBindReferences();
            EnsureTransparentButtonGraphic();
            BindPointerClickRelay();
        }

        public void SetupExpendable(
            ItemDefinition definition,
            int count,
            Sprite icon,
            UnityAction onSingleLeftClick)
        {
            AutoBindReferences();
            EnsureTransparentButtonGraphic();
            BindPointerClickRelay();

            CurrentItem = definition;
            CurrentKey = definition != null
                ? definition.Key
                : GameDatabase.NoneKey;
            singleLeftClickAction = onSingleLeftClick;

            if (definition == null)
            {
                gameObject.SetActive(false);
                return;
            }

            if (itemIcon != null)
            {
                itemIcon.sprite = icon;
                itemIcon.enabled = icon != null;
                itemIcon.gameObject.SetActive(icon != null);
                itemIcon.preserveAspect = true;
                itemIcon.raycastTarget = false;
            }

            if (nameText != null)
            {
                nameText.text = definition.name;
                nameText.raycastTarget = false;
            }

            if (typeText != null)
            {
                typeText.text = definition.type;
                typeText.raycastTarget = false;
            }

            if (countText != null)
            {
                int safeCount = Mathf.Max(0, count);
                countText.text = safeCount > 0 ? safeCount.ToString() : string.Empty;
                countText.gameObject.SetActive(safeCount > 0);
                countText.raycastTarget = false;
            }

            if (button != null)
            {
                button.interactable = true;
                button.onClick.RemoveAllListeners();
            }
        }

        public void Setup(
            ItemDefinition definition,
            int count,
            Sprite icon,
            UnityAction onSingleLeftClick)
        {
            SetupExpendable(definition, count, icon, onSingleLeftClick);
        }

        private void BindPointerClickRelay()
        {
            if (button == null)
                return;

            if (pointerClickRelay == null)
                pointerClickRelay = button.GetComponent<SlotPointerClickRelay>();

            if (pointerClickRelay == null)
                pointerClickRelay = button.gameObject.AddComponent<SlotPointerClickRelay>();

            pointerClickRelay.LeftClicked -= HandleSingleLeftClick;
            pointerClickRelay.LeftClicked += HandleSingleLeftClick;
        }

        private void OnDestroy()
        {
            if (pointerClickRelay != null)
                pointerClickRelay.LeftClicked -= HandleSingleLeftClick;
        }

        private void HandleSingleLeftClick()
        {
            ItemEquipmentInfoPopupUI.TryShow(CurrentKey);
            singleLeftClickAction?.Invoke();
        }

        private void EnsureTransparentButtonGraphic()
        {
            if (button == null)
                return;

            Image rootImage = button.targetGraphic as Image;

            if (rootImage == null)
                rootImage = GetComponent<Image>();

            if (rootImage == null)
                rootImage = gameObject.AddComponent<Image>();

            Color color = rootImage.color;
            color.a = 0f;
            rootImage.color = color;
            rootImage.enabled = true;
            rootImage.raycastTarget = true;

            button.targetGraphic = rootImage;
            button.transition = Selectable.Transition.None;
        }

        private void AutoBindReferences()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (itemIcon == null)
                itemIcon = FindImageByName("ItemIcon", "ExpendableItemIcon", "Icon");

            if (nameText == null)
                nameText = FindTextByName("ItemNameText", "NameText");

            if (countText == null)
                countText = FindTextByName("CountText", "QuantityText");

            if (typeText == null)
                typeText = FindTextByName("TypeText", "ItemTypeText");
        }

        private Image FindImageByName(params string[] targetNames)
        {
            Image[] images = GetComponentsInChildren<Image>(true);

            foreach (string targetName in targetNames)
            {
                foreach (Image image in images)
                {
                    if (image != null &&
                        image.transform != transform &&
                        image.name == targetName)
                    {
                        return image;
                    }
                }
            }

            return null;
        }

        private TMP_Text FindTextByName(params string[] targetNames)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

            foreach (string targetName in targetNames)
            {
                foreach (TMP_Text text in texts)
                {
                    if (text != null && text.name == targetName)
                        return text;
                }
            }

            return null;
        }
    }
}
