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

        public string CurrentKey { get; private set; }
            = GameDatabase.NoneKey;

        public ItemDefinition CurrentItem { get; private set; }

        private void Awake()
        {
            AutoBindReferences();
            EnsureTransparentButtonGraphic();
        }

        /// <summary>
        /// 소비 아이템 하나의 표시 정보와 클릭 이벤트를 설정합니다.
        /// </summary>
        public void SetupConsumable(
            ItemDefinition definition,
            int count,
            Sprite icon,
            UnityAction onClick)
        {
            AutoBindReferences();
            EnsureTransparentButtonGraphic();

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
                itemIcon.raycastTarget = false;
            }

            if (nameText != null)
            {
                nameText.text = definition != null
                    ? definition.name
                    : string.Empty;
            }

            if (typeText != null)
            {
                typeText.text = definition != null
                    ? definition.type
                    : string.Empty;
            }

            if (countText != null)
            {
                // 소비 아이템은 1개여도 수량을 보여주는 쪽이 알아보기 쉽습니다.
                countText.text = Mathf.Max(0, count).ToString();
                countText.gameObject.SetActive(count > 0);
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();

                if (onClick != null)
                    button.onClick.AddListener(onClick);
            }
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

        private void EnsureTransparentButtonGraphic()
        {
            if (button == null)
                return;

            if (button.targetGraphic != null)
                return;

            Image rootImage = GetComponent<Image>();

            if (rootImage == null)
                rootImage = gameObject.AddComponent<Image>();

            Color color = rootImage.color;
            color.a = 0f;

            rootImage.color = color;
            rootImage.raycastTarget = true;

            button.targetGraphic = rootImage;
            button.transition = Selectable.Transition.None;
        }

        private void AutoBindReferences()
        {
            if (button == null)
                button = GetComponent<Button>();

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