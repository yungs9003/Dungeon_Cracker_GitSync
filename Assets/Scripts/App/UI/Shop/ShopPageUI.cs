using System.Collections.Generic;
using Dungeon.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dungeon.UI.Shop
{
    public sealed class ShopPageUI : MonoBehaviour
    {
        [SerializeField] private ShopItemCardUI itemPrefab;
        [SerializeField] private ShopReceiptRowUI receiptPrefab;
        [SerializeField] private RectTransform productContent;
        [SerializeField] private RectTransform receiptContent;
        [SerializeField] private ScrollRect receiptScroll;
        [SerializeField] private TMP_Text goldLabel, totalLabel, feedbackLabel, commentLabel;
        [SerializeField] private Button checkoutButton, commentButton;
        [SerializeField] private ItemEquipmentInfoPopupUI infoPopup;
        [SerializeField] private string[] comments = { "그거 괜찮지?\n이번주 입고된 신상이야.", "마음에 드는 물건은\n두 번 눌러서 골라 봐.", "모험을 떠나기 전에\n필요한 물건을 챙겨 가." };
        private GameDatabase database;
        private StatusSaveFile saveFile;
        private readonly Dictionary<string, ItemDefinition> products = new();
        private readonly Dictionary<string, int> cart = new();
        private readonly List<string> cartOrder = new();
        private readonly Dictionary<string, ShopReceiptRowUI> receiptRows = new();
        private bool checkingOut;
        private int commentIndex;
        public IReadOnlyDictionary<string, int> Cart => cart;
        public long TotalGold { get; private set; }
        public bool CanCheckout => checkoutButton != null && checkoutButton.interactable;

        public void Configure(ShopItemCardUI card, ShopReceiptRowUI row, RectTransform items, RectTransform receipt,
            ScrollRect scroll, TMP_Text gold, TMP_Text total, TMP_Text feedback, Button checkout,
            Button nextComment, TMP_Text comment, ItemEquipmentInfoPopupUI popup)
        {
            itemPrefab = card; receiptPrefab = row; productContent = items; receiptContent = receipt;
            receiptScroll = scroll; goldLabel = gold; totalLabel = total; feedbackLabel = feedback;
            checkoutButton = checkout; commentButton = nextComment; commentLabel = comment; infoPopup = popup;
        }

        private void OnEnable()
        {
            if (checkoutButton != null) checkoutButton.onClick.AddListener(Checkout);
            if (commentButton != null) commentButton.onClick.AddListener(NextComment);
            Subscribe(); Refresh();
        }
        private void OnDisable()
        {
            if (checkoutButton != null) checkoutButton.onClick.RemoveListener(Checkout);
            if (commentButton != null) commentButton.onClick.RemoveListener(NextComment);
            Unsubscribe();
        }
        private void Start()
        {
            BindServices(GameDatabase.Instance, StatusSaveFile.Instance);
        }
        public void BindServices(GameDatabase db, StatusSaveFile save)
        {
            Unsubscribe(); database = db; saveFile = save; Subscribe();
            PopulateProducts(db != null ? db.Items : null);
            Refresh(); ShowComment();
        }

        /// <summary>편집 미리보기도 DB와 같은 순서로 상품을 구성합니다.</summary>
        public void PopulateProducts(IReadOnlyList<ItemDefinition> entries)
        {
            ClearChildren(productContent); products.Clear();
            if (entries == null) return;
            foreach (var item in entries)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.id) || products.ContainsKey(item.id)) continue;
                if (products.Count == 6) break;
                products.Add(item.id, item);
                var card = Instantiate(itemPrefab, productContent); card.name = $"Item_{item.id}";
                card.Bind(this, item, LoadItemIcon(item.id));
            }
        }
        public static Sprite LoadItemIcon(string id)
        {
            string path = "UI/ItemsIcons/" + id;
            Sprite icon = Resources.Load<Sprite>(path);
            if (icon != null) return icon;
            Sprite[] sprites = Resources.LoadAll<Sprite>(path);
            return sprites.Length > 0 ? sprites[0] : null;
        }
        public bool AddItem(string id)
        {
            if (checkingOut || !products.TryGetValue(id, out var item) || item.gold < 0) return false;
            cart.TryGetValue(id, out int quantity);
            if (quantity == int.MaxValue || TotalGold > int.MaxValue - (long)item.gold) return false;
            cart[id] = quantity + 1;
            bool isNew = quantity == 0;
            if (isNew) cartOrder.Add(id);
            Feedback(string.Empty); Refresh();
            if (isNew) receiptScroll.verticalNormalizedPosition = 0f;
            return true;
        }
        public bool RemoveItem(string id)
        {
            if (checkingOut || !cart.TryGetValue(id, out int quantity)) return false;
            if (quantity > 1) cart[id] = quantity - 1;
            else { cart.Remove(id); cartOrder.Remove(id); }
            Feedback(string.Empty); Refresh(); return true;
        }
        public void ShowItemInfo(string id)
        {
            if (infoPopup != null && products.TryGetValue(id, out var item)) infoPopup.ShowByKey(item.Key);
        }
        public void Checkout()
        {
            if (checkingOut || saveFile == null || database == null || cart.Count == 0) return;
            checkingOut = true; Refresh();
            try
            {
                if (saveFile.TryPurchaseItems(database, cart, out string error))
                { cart.Clear(); cartOrder.Clear(); Feedback("구매가 완료되었습니다."); }
                else Feedback(error);
            }
            finally { checkingOut = false; Refresh(); }
        }
        public void Refresh()
        {
            if (receiptContent == null || totalLabel == null || checkoutButton == null) return;
            var removed = new List<string>();
            foreach (var pair in receiptRows) if (!cart.ContainsKey(pair.Key)) removed.Add(pair.Key);
            foreach (string id in removed) { RemoveObject(receiptRows[id].gameObject); receiptRows.Remove(id); }
            TotalGold = 0;
            foreach (string id in cartOrder)
            {
                if (!cart.TryGetValue(id, out int quantity) || !products.TryGetValue(id, out var item)) continue;
                if (!receiptRows.TryGetValue(id, out var row))
                { row = Instantiate(receiptPrefab, receiptContent); row.name = $"Receipt_{id}"; receiptRows.Add(id, row); }
                row.Bind(item, quantity); TotalGold += (long)item.gold * quantity;
            }
            totalLabel.text = $"{TotalGold:N0}G";
            goldLabel.text = saveFile != null ? $"{saveFile.Data.gold:N0}G" : "— G";
            bool affordable = saveFile != null && saveFile.Data.gold >= TotalGold;
            checkoutButton.interactable = !checkingOut && database != null && cart.Count > 0 && affordable;
            checkoutButton.targetGraphic.color = checkoutButton.interactable ? new Color(.25f, .67f, .95f) : new Color(.55f, .55f, .55f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(receiptContent);
        }
        public void NextComment()
        { if (comments.Length > 0) commentIndex = (commentIndex + 1) % comments.Length; ShowComment(); }
        private void ShowComment()
        { if (commentLabel != null) commentLabel.text = comments.Length > 0 ? comments[commentIndex] : string.Empty; }
        private void Feedback(string value) { if (feedbackLabel != null) feedbackLabel.text = value; }
        private void Subscribe()
        { if (saveFile != null && isActiveAndEnabled) { saveFile.OnSaveDataChanged -= OnSaveChanged; saveFile.OnSaveDataChanged += OnSaveChanged; } }
        private void Unsubscribe() { if (saveFile != null) saveFile.OnSaveDataChanged -= OnSaveChanged; }
        private void OnSaveChanged(StatusSaveData _) => Refresh();
        private static void ClearChildren(Transform parent)
        { for (int i = parent.childCount - 1; i >= 0; i--) RemoveObject(parent.GetChild(i).gameObject); }
        private static void RemoveObject(GameObject obj)
        { if (Application.isPlaying) { obj.SetActive(false); Destroy(obj); } else DestroyImmediate(obj); }
    }
}
