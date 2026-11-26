using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DoomDriver.Economy;

namespace DoomDriver.Phone
{
    /// <summary>
    /// One upgrade card in the KurierShop app, styled like OrderCard (white rounded card, icon tile,
    /// mint price button whose outline thickens on focus). Implements IPhoneNavigable so
    /// PhoneInputNavigator can focus it and Enter buys it. Built in code by ShopAppUI.
    /// </summary>
    public class ShopItemView : MonoBehaviour, IPhoneNavigable, IPointerEnterHandler, IPointerClickHandler
    {
        private const int CARD_PADDING = 14;
        private const float CARD_SPACING = 14f;
        private const float ICON_SIZE = 72f;
        private const float ICON_INSET = 12f;
        private const float BUTTON_WIDTH = 104f;
        private const float BUTTON_HEIGHT = 46f;

        public event Action<IPhoneNavigable> OnHovered;

        public CourierUpgradeItem Item { get; private set; }

        private Action<CourierUpgradeItem> onPurchaseRequested;
        private Image buttonImage;
        private Outline buttonOutline;
        private TMP_Text buttonText;
        private TMP_Text chargesText;
        private bool isFocused;
        private float currentScale = 1f;

        /// <summary>
        /// Builds a complete card under parent and returns its view.
        /// </summary>
        public static ShopItemView Create(Transform parent, CourierUpgradeItem item, PhoneUIStyle.Assets style, float height, Action<CourierUpgradeItem> onPurchase)
        {
            var root = PhoneUIStyle.CreateRect($"ShopItem_{item.UpgradeType}", parent).gameObject;
            var view = root.AddComponent<ShopItemView>();
            view.Item = item;
            view.onPurchaseRequested = onPurchase;
            view.BuildLayout(style, height);
            return view;
        }

        private void BuildLayout(PhoneUIStyle.Assets style, float height)
        {
            PhoneUIStyle.AddCard(gameObject, style);
            gameObject.AddComponent<LayoutElement>().preferredHeight = height;

            var row = gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(CARD_PADDING, CARD_PADDING, CARD_PADDING, CARD_PADDING);
            row.spacing = CARD_SPACING;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            BuildIconTile(style, ICON_SIZE);
            BuildTextColumn(style);
            BuildPriceColumn(style);
        }

        private void BuildIconTile(PhoneUIStyle.Assets style, float size)
        {
            var tile = PhoneUIStyle.CreateRect("IconTile", transform);
            PhoneUIStyle.AddRoundedImage(tile.gameObject, style.SmallRadius, PhoneUIStyle.PillLight).raycastTarget = false;
            var layout = tile.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = size;
            layout.preferredHeight = size;

            if (Item.Icon != null)
            {
                var icon = PhoneUIStyle.CreateRect("Icon", tile).gameObject.AddComponent<Image>();
                icon.sprite = Item.Icon;
                icon.color = PhoneUIStyle.Indigo;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                Inset((RectTransform)icon.transform, ICON_INSET);
                return;
            }

            // No icon art yet: the item's initial in the same indigo as the order-card icons.
            var letter = PhoneUIStyle.CreateText(tile, style, size * 0.5f, FontStyles.Bold, TextAlignmentOptions.Center, PhoneUIStyle.Indigo, "Letter");
            letter.text = string.IsNullOrEmpty(Item.Name) ? "?" : Item.Name.Substring(0, 1);
            PhoneUIStyle.Stretch((RectTransform)letter.transform);
        }

        private void BuildTextColumn(PhoneUIStyle.Assets style)
        {
            var columnRect = PhoneUIStyle.CreateRect("TextColumn", transform);
            var column = columnRect.gameObject.AddComponent<VerticalLayoutGroup>();
            column.childAlignment = TextAnchor.MiddleLeft;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandHeight = false;
            column.spacing = 2f;
            columnRect.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            PhoneUIStyle.CreateText(columnRect, style, PhoneUIStyle.CARD_TITLE_SIZE, FontStyles.Bold, TextAlignmentOptions.Left, PhoneUIStyle.TextPrimary, "Title").text = Item.Name;
            PhoneUIStyle.CreateText(columnRect, style, PhoneUIStyle.SMALL_SIZE, FontStyles.Normal, TextAlignmentOptions.TopLeft, PhoneUIStyle.TextMuted, "Description").text = Item.Description;
        }

        private void BuildPriceColumn(PhoneUIStyle.Assets style)
        {
            var columnRect = PhoneUIStyle.CreateRect("PriceColumn", transform);
            var column = columnRect.gameObject.AddComponent<VerticalLayoutGroup>();
            column.childAlignment = TextAnchor.MiddleCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandHeight = false;
            column.spacing = 4f;
            columnRect.gameObject.AddComponent<LayoutElement>().preferredWidth = BUTTON_WIDTH;

            var buttonRect = PhoneUIStyle.CreateRect("PriceButton", columnRect);
            buttonImage = PhoneUIStyle.AddRoundedImage(buttonRect.gameObject, style.SmallRadius, PhoneUIStyle.ActionButton);
            buttonOutline = buttonRect.gameObject.AddComponent<Outline>();
            buttonOutline.effectColor = PhoneUIStyle.ButtonOutline;
            buttonOutline.effectDistance = PhoneUIStyle.ButtonOutlineNormal;
            buttonRect.gameObject.AddComponent<LayoutElement>().preferredHeight = BUTTON_HEIGHT;

            buttonText = PhoneUIStyle.CreateText(buttonRect, style, PhoneUIStyle.BUTTON_TEXT_SIZE, FontStyles.Bold, TextAlignmentOptions.Center, PhoneUIStyle.IndigoDark, "Label");
            PhoneUIStyle.Stretch((RectTransform)buttonText.transform);

            chargesText = PhoneUIStyle.CreateText(columnRect, style, PhoneUIStyle.SMALL_SIZE, FontStyles.Bold, TextAlignmentOptions.Center, PhoneUIStyle.Indigo, "Charges");
        }

        /// <summary>
        /// Updates the button (price / OWNED / unaffordable) and the consumable charge count.
        /// </summary>
        public void Refresh(bool isOwned, int charges, bool canAfford)
        {
            bool isOwnedForever = isOwned && !Item.IsConsumable;
            bool hasCharges = Item.IsConsumable && charges > 0;
            chargesText.gameObject.SetActive(hasCharges);
            if (hasCharges) chargesText.text = $"x{charges} in bag";

            if (isOwnedForever)
            {
                buttonImage.color = PhoneUIStyle.Pill;
                buttonText.text = "OWNED";
                buttonText.color = PhoneUIStyle.Indigo;
                return;
            }

            buttonText.text = $"{Item.Price:F0} €";
            buttonImage.color = canAfford ? PhoneUIStyle.ActionButton : PhoneUIStyle.Disabled;
            buttonText.color = canAfford ? PhoneUIStyle.IndigoDark : PhoneUIStyle.Negative;
        }

        // Navigator never unfocuses old items when the panel closes; reset so reopening shows one highlight.
        private void OnDisable()
        {
            SetFocused(false);
            currentScale = 1f;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            float target = isFocused ? PhoneUIStyle.FOCUSED_SCALE : 1f;
            if (Mathf.Abs(currentScale - target) <= 0.001f) return;

            currentScale = Mathf.Lerp(currentScale, target, Time.unscaledDeltaTime * PhoneUIStyle.SCALE_SPEED);
            transform.localScale = Vector3.one * currentScale;
        }

        public void SetFocused(bool focused)
        {
            isFocused = focused;
            if (buttonOutline != null)
            {
                buttonOutline.effectDistance = focused ? PhoneUIStyle.ButtonOutlineFocused : PhoneUIStyle.ButtonOutlineNormal;
            }
        }

        public void Select()
        {
            onPurchaseRequested?.Invoke(Item);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnHovered?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Select();
        }

        private static void Inset(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
