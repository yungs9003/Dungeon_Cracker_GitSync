using Dungeon.Core;
using TMPro;
using UnityEngine;

namespace Dungeon.UI.Shop
{
    public sealed class ShopReceiptRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text quantityLabel;
        [SerializeField] private TMP_Text amountLabel;
        public string ItemId { get; private set; }
        public int Quantity { get; private set; }
        public void Configure(TMP_Text title, TMP_Text quantity, TMP_Text amount)
        { titleLabel = title; quantityLabel = quantity; amountLabel = amount; }
        public void Bind(ItemDefinition item, int quantity)
        {
            ItemId = item.id; Quantity = quantity;
            titleLabel.text = $"{item.id} {item.name}";
            quantityLabel.text = quantity.ToString("N0");
            amountLabel.text = $"{(long)item.gold * quantity:N0}G";
        }
    }
}
