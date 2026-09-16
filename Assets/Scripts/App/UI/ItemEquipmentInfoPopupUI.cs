using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Dungeon.Core;

namespace Dungeon.UI
{
    /// <summary>
    /// Shared popup for item/equipment definitions stored in db.json.
    /// The view stays alive while its content root is hidden, allowing any slot
    /// to find and open it without keeping a scene-specific reference.
    /// </summary>
    public class ItemEquipmentInfoPopupUI : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private GameDatabase database;

        [Header("View")]
        [SerializeField] private GameObject popupContent;
        [SerializeField] private Button dismissButton;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image typeTagImage;
        [SerializeField] private Image rankTagImage;

        [Header("Item Type Tags")]
        [SerializeField] private Sprite consumableTag;
        [SerializeField] private Sprite materialTag;
        [SerializeField] private Sprite keyTag;

        [Header("Equipment Slot Tags")]
        [SerializeField] private Sprite headTag;
        [SerializeField] private Sprite bodyTag;
        [SerializeField] private Sprite legsTag;

        [Header("Rank Tags (index = rank)")]
        [SerializeField] private Sprite[] rankTags = new Sprite[5];

        public static ItemEquipmentInfoPopupUI Instance { get; private set; }
        public bool IsOpen => popupContent != null && popupContent.activeSelf;

        private void Awake()
        {
            Instance = this;
            ApplyReferencePresentation();

            if (dismissButton != null)
                dismissButton.onClick.AddListener(Hide);

            Hide();
        }

        private void OnEnable()
        {
            Instance = this;
            ApplyReferencePresentation();
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnDestroy()
        {
            if (dismissButton != null)
                dismissButton.onClick.RemoveListener(Hide);

            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
                Hide();
        }

        public static bool TryShow(string databaseKey)
        {
            ItemEquipmentInfoPopupUI popup = Instance;

            if (popup == null)
            {
                popup = FindFirstObjectByType<ItemEquipmentInfoPopupUI>(
                    FindObjectsInactive.Include
                );
            }

            if (popup == null)
            {
                Debug.LogWarning(
                    "[ItemEquipmentInfoPopupUI] Popup instance was not found in the loaded scene."
                );
                return false;
            }

            return popup.ShowByKey(databaseKey);
        }

        public bool ShowByKey(string databaseKey)
        {
            ApplyReferencePresentation();

            if (database == null)
                database = GameDatabase.Instance;

            if (database == null)
            {
                Debug.LogWarning(
                    "[ItemEquipmentInfoPopupUI] GameDatabase is not available."
                );
                return false;
            }

            string normalizedKey = GameDatabase.NormalizeKey(databaseKey);

            if (!GameDatabase.TryParseKey(
                    normalizedKey,
                    out string category,
                    out _))
            {
                Debug.LogWarning(
                    $"[ItemEquipmentInfoPopupUI] Invalid database key: {databaseKey}"
                );
                return false;
            }

            string displayName;
            string description;
            string classification;
            int rank;
            bool isEquipment;

            if (category == GameDatabase.CategoryEquip &&
                database.TryGetEquipmentByKey(
                    normalizedKey,
                    out EquipmentDefinition equipment))
            {
                displayName = equipment.name;
                description = equipment.desc;
                classification = equipment.slot;
                rank = equipment.rank;
                isEquipment = true;
            }
            else if (category == GameDatabase.CategoryItem &&
                     database.TryGetItemByKey(
                         normalizedKey,
                         out ItemDefinition item))
            {
                displayName = item.name;
                description = item.desc;
                classification = item.type;
                rank = item.rank;
                isEquipment = false;
            }
            else
            {
                Debug.LogWarning(
                    $"[ItemEquipmentInfoPopupUI] Definition was not found: {normalizedKey}"
                );
                return false;
            }

            if (categoryText != null)
                categoryText.text = isEquipment ? "-EQUIPMENT-" : "-ITEM-";

            if (nameText != null)
                nameText.text = displayName ?? string.Empty;

            if (descriptionText != null)
                descriptionText.text = description ?? string.Empty;

            SetImageSprite(
                itemIcon,
                database.GetIconByKey(normalizedKey)
            );

            SetImageSprite(
                typeTagImage,
                isEquipment
                    ? GetEquipmentSlotTag(classification)
                    : GetItemTypeTag(classification)
            );

            SetImageSprite(rankTagImage, GetRankTag(rank));

            if (popupContent != null)
            {
                transform.SetAsLastSibling();
                popupContent.SetActive(true);
                popupContent.transform.SetAsLastSibling();
            }

            return true;
        }

        public void Hide()
        {
            if (popupContent != null)
                popupContent.SetActive(false);
        }

        public void Configure(
            GameDatabase configuredDatabase,
            GameObject configuredPopupContent,
            Button configuredDismissButton,
            Image configuredItemIcon,
            TMP_Text configuredCategoryText,
            TMP_Text configuredNameText,
            TMP_Text configuredDescriptionText,
            Image configuredTypeTagImage,
            Image configuredRankTagImage,
            Sprite configuredConsumableTag,
            Sprite configuredMaterialTag,
            Sprite configuredKeyTag,
            Sprite configuredHeadTag,
            Sprite configuredBodyTag,
            Sprite configuredLegsTag,
            Sprite[] configuredRankTags)
        {
            database = configuredDatabase;
            popupContent = configuredPopupContent;
            dismissButton = configuredDismissButton;
            itemIcon = configuredItemIcon;
            categoryText = configuredCategoryText;
            nameText = configuredNameText;
            descriptionText = configuredDescriptionText;
            typeTagImage = configuredTypeTagImage;
            rankTagImage = configuredRankTagImage;
            consumableTag = configuredConsumableTag;
            materialTag = configuredMaterialTag;
            keyTag = configuredKeyTag;
            headTag = configuredHeadTag;
            bodyTag = configuredBodyTag;
            legsTag = configuredLegsTag;
            rankTags = configuredRankTags ?? new Sprite[0];
            ApplyReferencePresentation();
        }

        /// <summary>
        /// Keeps both the standalone popup prefab and the copy embedded in
        /// SharedStatus aligned to the supplied 120 x 126 SVG (rendered at 3x).
        /// This intentionally changes presentation only; lookup and click
        /// behaviour remain owned by the existing inventory/equipment flow.
        /// </summary>
        private void ApplyReferencePresentation()
        {
            RectTransform panelRect = ResolvePanelRect();
            if (panelRect == null)
                return;

            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(360f, 378f);
            panelRect.localScale = Vector3.one;

            Image panelImage = panelRect.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = Color.white;
                panelImage.type = Image.Type.Simple;
                panelImage.preserveAspect = false;
                panelImage.raycastTarget = true;
            }

            ConfigureText(
                categoryText,
                new Vector2(360f, 45f),
                Vector2.zero,
                24f,
                Color.white,
                TextAlignmentOptions.Center,
                TextWrappingModes.NoWrap,
                TextOverflowModes.Overflow);

            if (categoryText != null)
                categoryText.characterSpacing = 8f;

            ConfigureImage(itemIcon, new Vector2(78f, 78f), new Vector2(45f, 78f));

            ConfigureText(
                nameText,
                new Vector2(195f, 58f),
                new Vector2(142f, 53f),
                39f,
                new Color32(108, 172, 255, 255),
                TextAlignmentOptions.Center,
                TextWrappingModes.NoWrap,
                TextOverflowModes.Ellipsis);

            ConfigureImage(typeTagImage, new Vector2(63f, 27f), new Vector2(159f, 108f));
            ConfigureImage(rankTagImage, new Vector2(84f, 27f), new Vector2(159f, 144f));

            RectTransform divider = panelRect.Find("Divider") as RectTransform;
            if (divider != null)
            {
                SetTopLeftRect(divider, new Vector2(294f, 3f), new Vector2(33f, 175.5f));
                Image dividerImage = divider.GetComponent<Image>();
                if (dividerImage != null)
                {
                    dividerImage.color = new Color32(217, 217, 217, 255);
                    dividerImage.raycastTarget = false;
                }
            }

            ConfigureText(
                descriptionText,
                new Vector2(294f, 165f),
                new Vector2(33f, 195f),
                24f,
                Color.black,
                TextAlignmentOptions.TopLeft,
                TextWrappingModes.Normal,
                TextOverflowModes.Ellipsis);

            if (descriptionText != null)
                descriptionText.lineSpacing = 4f;
        }

        private RectTransform ResolvePanelRect()
        {
            if (nameText != null && nameText.rectTransform.parent is RectTransform parent)
                return parent;

            return popupContent != null
                ? popupContent.transform.Find("PopupPanel") as RectTransform
                : null;
        }

        private static void ConfigureText(
            TMP_Text text,
            Vector2 size,
            Vector2 topLeft,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            TextWrappingModes wrapping,
            TextOverflowModes overflow)
        {
            if (text == null)
                return;

            SetTopLeftRect(text.rectTransform, size, topLeft);
            text.enableAutoSizing = false;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Normal;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = wrapping;
            text.overflowMode = overflow;
            text.richText = true;
            text.raycastTarget = false;

            if (text.font != null)
                text.font.isMultiAtlasTexturesEnabled = true;
        }

        private static void ConfigureImage(Image image, Vector2 size, Vector2 topLeft)
        {
            if (image == null)
                return;

            SetTopLeftRect(image.rectTransform, size, topLeft);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private static void SetTopLeftRect(
            RectTransform rect,
            Vector2 size,
            Vector2 topLeft)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
            rect.localScale = Vector3.one;
        }

        private Sprite GetEquipmentSlotTag(string slot)
        {
            return GameDatabase.NormalizeSlot(slot) switch
            {
                "head" => headTag,
                "body" => bodyTag,
                "legs" => legsTag,
                _ => null
            };
        }

        private Sprite GetItemTypeTag(string type)
        {
            return GameDatabase.NormalizeCategoryLikeValue(type) switch
            {
                "consumable" => consumableTag,
                "material" => materialTag,
                "key" => keyTag,
                _ => null
            };
        }

        private Sprite GetRankTag(int rank)
        {
            if (rankTags == null || rank < 0 || rank >= rankTags.Length)
                return null;

            return rankTags[rank];
        }

        private static void SetImageSprite(Image image, Sprite sprite)
        {
            if (image == null)
                return;

            image.sprite = sprite;
            image.enabled = sprite != null;
            image.preserveAspect = true;
        }
    }
}
