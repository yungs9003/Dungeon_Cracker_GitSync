using System.Collections;
using Dungeon.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dungeon.UI.Shop
{
    public sealed class ShopItemCardUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text priceLabel;
        [SerializeField, Min(.1f)] private float doubleClickWindow = .3f;
        private ShopPageUI page;
        private Coroutine pendingClick;
        private float firstClickTime;
        public ItemDefinition Item { get; private set; }

        public void Configure(Image image, TMP_Text nameText, TMP_Text priceText)
        { icon = image; nameLabel = nameText; priceLabel = priceText; }

        public void Bind(ShopPageUI owner, ItemDefinition item, Sprite sprite)
        {
            page = owner; Item = item;
            nameLabel.text = item.name;
            priceLabel.text = item.gold >= 0 ? $"{item.gold:N0}G" : "판매 불가";
            icon.sprite = sprite; icon.enabled = sprite != null;
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (page == null || Item == null || data.dragging) return;
            if (data.button == PointerEventData.InputButton.Right)
            { CancelClick(); page.RemoveItem(Item.id); return; }
            if (data.button != PointerEventData.InputButton.Left) return;
            // 연속 네 번 클릭도 두 쌍으로 처리하여 두 개만 추가합니다.
            if (pendingClick != null && Time.unscaledTime - firstClickTime <= doubleClickWindow)
            { CancelClick(); page.AddItem(Item.id); return; }
            CancelClick(); firstClickTime = Time.unscaledTime;
            pendingClick = StartCoroutine(ShowInfoAfterDoubleClickWindow());
        }

        private IEnumerator ShowInfoAfterDoubleClickWindow()
        {
            yield return new WaitForSecondsRealtime(doubleClickWindow);
            pendingClick = null;
            page.ShowItemInfo(Item.id);
        }
        private void OnDisable() => CancelClick();
        private void CancelClick()
        { if (pendingClick != null) StopCoroutine(pendingClick); pendingClick = null; }
    }
}
