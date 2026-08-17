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

            if (dismissButton != null)
                dismissButton.onClick.AddListener(Hide);

            Hide();
        }

        private void OnEnable()
        {
            Instance = this;
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
                categoryText.text = isEquipment ? "-Equipment-" : "-ITEM-";

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
