using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Dungeon.Core;

namespace Dungeon.UI
{
    /// <summary>
    /// 소비 아이템 목록에 표시되는 아이템 한 칸입니다.
    ///
    /// 이 클래스는 아이템 표시와 클릭 전달만 담당합니다.
    /// 실제 퀵슬롯 등록은 ConsumablePageUI가 처리합니다.
    /// </summary>
    public class ConsumableItemSlotUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button button;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text typeText;

        [Tooltip("이 아이템이 하나 이상의 퀵슬롯에 등록되어 있을 때 E를 표시합니다.")]
        [SerializeField] private TMP_Text equippedMarkText;

        [Header("Debug")]
        [SerializeField] private bool logClick = true;

        public string CurrentKey { get; private set; }
            = GameDatabase.NoneKey;

        public ItemDefinition CurrentItem { get; private set; }

        private void Awake()
        {
            AutoBindReferences();
            EnsureButtonGraphic();
        }

        /// <summary>
        /// 소비 아이템 하나의 표시 정보와 클릭 이벤트를 설정합니다.
        /// </summary>
        public void SetupConsumable(
            ItemDefinition definition,
            int count,
            Sprite icon,
            bool isEquipped,
            UnityAction onClick)
        {
            AutoBindReferences();
            EnsureButtonGraphic();

            CurrentItem = definition;
            CurrentKey = definition != null
                ? definition.Key
                : GameDatabase.NoneKey;

            if (itemIcon != null)
            {
                itemIcon.sprite = icon;
                itemIcon.enabled = icon != null;
                itemIcon.gameObject.SetActive(icon != null);
                itemIcon.preserveAspect = true;

                // ItemIcon 자체가 Button의 Target Graphic인 구조라면
                // raycastTarget을 꺼 버리면 버튼 클릭도 함께 막힙니다.
                // 별도의 버튼 Graphic을 사용하는 경우에만 아이콘의 Raycast를 끕니다.
                bool iconIsButtonGraphic =
                    button != null &&
                    button.targetGraphic == itemIcon;

                itemIcon.raycastTarget = iconIsButtonGraphic;
            }

            if (nameText != null)
            {
                nameText.text = definition != null
                    ? definition.name
                    : string.Empty;
                nameText.raycastTarget = false;
            }

            if (typeText != null)
            {
                typeText.text = definition != null
                    ? definition.type
                    : string.Empty;
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
                    $"[ConsumableItemSlotUI] Button reference is missing: " +
                    $"{gameObject.name}"
                );
                return;
            }

            // E가 표시되는 아이템은 이미 어느 퀵슬롯엔가 등록된 상태입니다.
            // 기존 슬롯을 우클릭해 해제하기 전에는 다른 슬롯에 중복 등록할 수 없습니다.
            button.interactable = !isEquipped;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (logClick)
                {
                    Debug.Log(
                        $"[ConsumableItemSlotUI] Inventory slot clicked. " +
                        $"key={CurrentKey}, " +
                        $"name={(CurrentItem != null ? CurrentItem.name : "null")}, " +
                        $"buttonObject={button.gameObject.name}"
                    );
                }

                onClick?.Invoke();
            });

            if (!button.interactable)
            {
                Debug.LogWarning(
                    $"[ConsumableItemSlotUI] Button is not interactable: " +
                    $"{button.gameObject.name}"
                );
            }
        }

        // 기존 호출부 호환용입니다.
        public void SetupConsumable(
            ItemDefinition definition,
            int count,
            Sprite icon,
            UnityAction onClick)
        {
            SetupConsumable(
                definition,
                count,
                icon,
                false,
                onClick
            );
        }

        // Setup이라는 이름으로도 호출할 수 있게 한 호환용 메서드입니다.
        public void Setup(
            ItemDefinition definition,
            int count,
            Sprite icon,
            UnityAction onClick)
        {
            SetupConsumable(definition, count, icon, onClick);
        }

        public void SetEquippedMark(bool isEquipped)
        {
            if (equippedMarkText == null)
                return;

            equippedMarkText.text = "E";
            equippedMarkText.raycastTarget = false;
            equippedMarkText.gameObject.SetActive(isEquipped);
        }

        /// <summary>
        /// Button 오브젝트 자신의 Image를 클릭 판정용 Graphic으로 사용합니다.
        ///
        /// 이전 코드처럼 ConsumableItemSlotUI가 붙은 오브젝트의 Image를 사용하면,
        /// Button이 자식 오브젝트에 있는 프리팹 구조에서는 클릭 이벤트가
        /// Button까지 전달되지 않을 수 있습니다.
        /// </summary>
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

            // ConsumableItemSlotUI와 Button이 서로 다른 오브젝트에
            // 붙어 있는 프리팹 구조도 지원합니다.
            if (button == null)
                button = GetComponentInChildren<Button>(true);

            if (itemIcon == null)
            {
                itemIcon = FindImageByName(
                    "ItemIcon",
                    "ConsumableItemIcon",
                    "Icon"
                );
            }

            if (nameText == null)
            {
                nameText = FindTextByName(
                    "ItemNameText",
                    "NameText"
                );
            }

            if (countText == null)
            {
                countText = FindTextByName(
                    "CountText",
                    "QuantityText"
                );
            }

            if (typeText == null)
            {
                typeText = FindTextByName(
                    "TypeText",
                    "ItemTypeText"
                );
            }

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
            TMP_Text[] texts =
                GetComponentsInChildren<TMP_Text>(true);

            foreach (string targetName in names)
            {
                foreach (TMP_Text text in texts)
                {
                    if (text != null &&
                        text.name == targetName)
                    {
                        return text;
                    }
                }
            }

            return null;
        }
    }
}
