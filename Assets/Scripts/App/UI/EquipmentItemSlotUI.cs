using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Dungeon.Core;

namespace Dungeon.UI
{
    /// <summary>
    /// Scroll View에 표시되는 장비 아이템 1칸입니다.
    /// currentKey는 equip:000 같은 category key입니다.
    /// </summary>
    public class EquipmentItemSlotUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Button button;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text slotText;

        [Tooltip("현재 장착 중인 장비라면 E를 표시합니다.")]
        [SerializeField] private TMP_Text equippedMarkText;

        [Header("Debug")]
        [SerializeField] private bool logClick;

        public string CurrentKey { get; private set; }
        public EquipmentDefinition CurrentEquipment { get; private set; }

        private void Awake()
        {
            AutoBindReferences();
            EnsureTransparentButtonGraphic();
        }

        public void SetupEquipment(
            EquipmentDefinition definition,
            int count,
            Sprite icon,
            bool isEquipped,
            UnityAction onClick)
        {
            AutoBindReferences();
            EnsureTransparentButtonGraphic();

            CurrentEquipment = definition;
            CurrentKey = definition != null ? definition.Key : GameDatabase.NoneKey;

            if (definition == null)
            {
                gameObject.SetActive(false);
                return;
            }

            if (itemIcon != null)
            {
                itemIcon.sprite = icon;
                itemIcon.enabled = icon != null;
                itemIcon.preserveAspect = true;
                itemIcon.raycastTarget = false;
            }

            if (nameText != null)
                nameText.text = definition.name;

            if (slotText != null)
                slotText.text = definition.slot;

            if (countText != null)
            {
                countText.text = count > 1 ? count.ToString() : string.Empty;
                countText.gameObject.SetActive(count > 1);
            }

            SetEquippedMark(isEquipped);

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    if (logClick)
                    {
                        Debug.Log(
                            $"[EquipmentItemSlotUI] Clicked. " +
                            $"key={CurrentKey}, id={CurrentEquipment.id}, slot={CurrentEquipment.slot}, name={CurrentEquipment.name}"
                        );
                    }

                    onClick?.Invoke();
                });
            }
            else
            {
                Debug.LogWarning($"[EquipmentItemSlotUI] Button reference is missing: {gameObject.name}");
            }
        }

        // 기존 호출부 호환용입니다.
        public void SetupEquipment(
            EquipmentDefinition definition,
            int count,
            Sprite icon,
            UnityAction onClick)
        {
            SetupEquipment(
                definition,
                count,
                icon,
                false,
                onClick
            );
        }

        // 이전 버전 호환용입니다.
        public void Setup(EquipmentDefinition definition, int count, Sprite icon, UnityAction onClick)
        {
            SetupEquipment(definition, count, icon, onClick);
        }

        public void SetEquippedMark(bool isEquipped)
        {
            if (equippedMarkText == null)
                return;

            equippedMarkText.text = "E";
            equippedMarkText.raycastTarget = false;
            equippedMarkText.gameObject.SetActive(isEquipped);
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

            Color c = rootImage.color;
            c.a = 0f;
            rootImage.color = c;
            rootImage.raycastTarget = true;
            rootImage.enabled = true;

            button.targetGraphic = rootImage;
            button.transition = Selectable.Transition.None;
        }

        private void AutoBindReferences()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (itemIcon == null)
                itemIcon = FindImageByName("ItemIcon", "EquipItemIcon", "Icon");

            if (nameText == null)
                nameText = FindTextByName("ItemNameText", "NameText");

            if (countText == null)
                countText = FindTextByName("CountText", "QuantityText");

            if (slotText == null)
                slotText = FindTextByName("SlotText", "TypeText");

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
            foreach (string target in names)
            {
                foreach (var image in images)
                {
                    if (image != null && image.name == target)
                        return image;
                }
            }
            return null;
        }

        private TMP_Text FindTextByName(params string[] names)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            foreach (string target in names)
            {
                foreach (var text in texts)
                {
                    if (text != null && text.name == target)
                        return text;
                }
            }
            return null;
        }
    }
}
