using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Dungeon.Core;

namespace Dungeon.UI
{
    /// <summary>
    /// Displays one consumable item and routes single/double/right click actions.
    /// </summary>
    public class ConsumableItemSlotUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button button;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text typeText;
        [SerializeField] private TMP_Text equippedMarkText;

        [Header("Debug")]
        [SerializeField] private bool logClick = true;

        private SlotPointerClickRelay pointerClickRelay;
        private UnityAction doubleLeftClickAction;
        private UnityAction rightClickAction;

        public string CurrentKey { get; private set; } = GameDatabase.NoneKey;
        public ItemDefinition CurrentItem { get; private set; }

        private void Awake()
        {
            AutoBindReferences();
            EnsureButtonGraphic();
            BindPointerClickRelay();
        }

        public void SetupConsumable(
            ItemDefinition definition,
            int count,
            Sprite icon,
            bool isEquipped,
            UnityAction onDoubleLeftClick,
            UnityAction onRightClick)
        {
            AutoBindReferences();
            EnsureButtonGraphic();
            BindPointerClickRelay();

            CurrentItem = definition;
            CurrentKey = definition != null
                ? definition.Key
                : GameDatabase.NoneKey;
            doubleLeftClickAction = onDoubleLeftClick;
            rightClickAction = onRightClick;

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
                countText.text = Mathf.Max(0, count).ToString();
                countText.gameObject.SetActive(count > 0);
                countText.raycastTarget = false;
            }

            SetEquippedMark(isEquipped);

            if (button == null)
            {
                Debug.LogWarning(
                    $"[ConsumableItemSlotUI] Button reference is missing: {gameObject.name}"
                );
                return;
            }

            // An equipped entry must remain clickable for popup and right-click removal.
            button.interactable = true;
            button.onClick.RemoveAllListeners();
        }

        public void SetupConsumable(
            ItemDefinition definition,
            int count,
            Sprite icon,
            bool isEquipped,
            UnityAction onDoubleLeftClick)
        {
            SetupConsumable(
                definition,
                count,
                icon,
                isEquipped,
                onDoubleLeftClick,
                null
            );
        }

        public void SetupConsumable(
            ItemDefinition definition,
            int count,
            Sprite icon,
            UnityAction onDoubleLeftClick)
        {
            SetupConsumable(
                definition,
                count,
                icon,
                false,
                onDoubleLeftClick,
                null
            );
        }

        public void Setup(
            ItemDefinition definition,
            int count,
            Sprite icon,
            UnityAction onDoubleLeftClick)
        {
            SetupConsumable(definition, count, icon, onDoubleLeftClick);
        }

        public void SetEquippedMark(bool isEquipped)
        {
            if (equippedMarkText == null)
                return;

            equippedMarkText.text = "E";
            equippedMarkText.raycastTarget = false;
            equippedMarkText.gameObject.SetActive(isEquipped);
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
            pointerClickRelay.DoubleLeftClicked -= HandleDoubleLeftClick;
            pointerClickRelay.RightClicked -= HandleRightClick;

            pointerClickRelay.LeftClicked += HandleSingleLeftClick;
            pointerClickRelay.DoubleLeftClicked += HandleDoubleLeftClick;
            pointerClickRelay.RightClicked += HandleRightClick;
        }

        private void OnDestroy()
        {
            if (pointerClickRelay == null)
                return;

            pointerClickRelay.LeftClicked -= HandleSingleLeftClick;
            pointerClickRelay.DoubleLeftClicked -= HandleDoubleLeftClick;
            pointerClickRelay.RightClicked -= HandleRightClick;
        }

        private void HandleSingleLeftClick()
        {
            if (logClick)
                Debug.Log($"[ConsumableItemSlotUI] Single click: {CurrentKey}");

            ItemEquipmentInfoPopupUI.TryShow(CurrentKey);
        }

        private void HandleDoubleLeftClick()
        {
            if (logClick)
                Debug.Log($"[ConsumableItemSlotUI] Double click: {CurrentKey}");

            doubleLeftClickAction?.Invoke();
        }

        private void HandleRightClick()
        {
            rightClickAction?.Invoke();
        }

        private void EnsureButtonGraphic()
        {
            if (button == null)
                return;

            Image buttonImage = button.GetComponent<Image>();

            if (buttonImage == null)
            {
                buttonImage = button.gameObject.AddComponent<Image>();
                buttonImage.color = new Color(1f, 1f, 1f, 0f);
            }

            buttonImage.enabled = true;
            buttonImage.raycastTarget = true;
            button.targetGraphic = buttonImage;
            button.transition = Selectable.Transition.None;
        }

        private void AutoBindReferences()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (button == null)
                button = GetComponentInChildren<Button>(true);

            if (itemIcon == null)
                itemIcon = FindImageByName("ItemIcon", "ConsumableItemIcon", "Icon");

            if (nameText == null)
                nameText = FindTextByName("ItemNameText", "NameText");

            if (countText == null)
                countText = FindTextByName("CountText", "QuantityText");

            if (typeText == null)
                typeText = FindTextByName("TypeText", "ItemTypeText");

            if (equippedMarkText == null)
            {
                equippedMarkText = FindTextByName(
                    "EquippedMarkText",
                    "EquippedMark",
                    "EquippedText",
                    "EText"
                );
            }
        }

        private Image FindImageByName(params string[] names)
        {
            Image[] images = GetComponentsInChildren<Image>(true);

            foreach (string targetName in names)
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

        private TMP_Text FindTextByName(params string[] names)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

            foreach (string targetName in names)
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
