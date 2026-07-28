using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Dungeon.Core;

namespace Dungeon.UI
{
    /// <summary>
    /// ExpendablesPage의 Scroll View에 표시되는 아이템 한 칸입니다.
    ///
    /// 역할:
    /// - 아이템 아이콘 표시
    /// - 아이템 이름, 타입, 보유 수량 표시
    /// - Button 클릭 콜백 보관
    ///
    /// 실제 목록 생성과 필터링은 ExpendablesPageUI가 담당합니다.
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

        public string CurrentKey { get; private set; }
            = GameDatabase.NoneKey;

        public ItemDefinition CurrentItem { get; private set; }

        private void Awake()
        {
            AutoBindReferences();
            EnsureTransparentButtonGraphic();
        }

        /// <summary>
        /// 슬롯에 표시할 일반 아이템 정보를 설정합니다.
        ///
        /// onClick에 null을 전달해도 Button 컴포넌트는 유지됩니다.
        /// 나중에 기능을 추가할 때 콜백만 전달하면 됩니다.
        /// </summary>
        public void SetupExpendable(
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
                nameText.text = definition != null
                    ? definition.name
                    : string.Empty;

                typeText.text = definition != null
                    ? definition.type
                    : string.Empty;
            }

            if (countText != null)
            {
                int safeCount = Mathf.Max(0, count);

                countText.text = safeCount > 0
                    ? safeCount.ToString()
                    : string.Empty;

                countText.gameObject.SetActive(
                    safeCount > 0
                );
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();

                // 지금은 null을 전달해도 됩니다.
                // 나중에 기능이 생기면 콜백이 실행됩니다.
                if (onClick != null)
                    button.onClick.AddListener(onClick);
            }
        }

        /// <summary>
        /// EquipmentItemSlotUI의 Setup과 비슷하게 사용할 수 있는
        /// 호환용 메서드입니다.
        /// </summary>
        public void Setup(
            ItemDefinition definition,
            int count,
            Sprite icon,
            UnityAction onClick)
        {
            SetupExpendable(
                definition,
                count,
                icon,
                onClick
            );
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
                    "ExpendableItemIcon",
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

        private Image FindImageByName(
            params string[] targetNames)
        {
            Image[] images =
                GetComponentsInChildren<Image>(true);

            foreach (string targetName in targetNames)
            {
                foreach (Image image in images)
                {
                    if (image == null)
                        continue;

                    if (image.transform == transform)
                        continue;

                    if (image.name == targetName)
                        return image;
                }
            }

            return null;
        }

        private TMP_Text FindTextByName(
            params string[] targetNames)
        {
            TMP_Text[] texts =
                GetComponentsInChildren<TMP_Text>(true);

            foreach (string targetName in targetNames)
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