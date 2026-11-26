using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DoomDriver.Economy;

namespace DoomDriver.Phone
{
    /// <summary>
    /// KurierShop app on the phone (lives on ShopScreenPanel, opened via PhoneOSController.ShopApp).
    /// Lists UpgradeManager.Catalog as ShopItemView rows and buys items through UpgradeManager,
    /// which deducts money via WalletManager. PhoneOSController collects the rows as IPhoneNavigable
    /// when the shop opens, so keyboard (WASD/Enter/ESC) and mouse both work.
    /// </summary>
    public class ShopAppUI : MonoBehaviour
    {
        private const float CONTAINER_TOP_OFFSET = 230f;
        private const float CONTAINER_SIDE_OFFSET = 20f; // same margin as Courier CardsContainer
        private const float CONTAINER_BOTTOM_OFFSET = 70f;
        private const float STATUS_HEIGHT = 50f;
        private const float STATUS_FONT_SIZE = 20f;
        private const float ROW_SPACING = 14f;
        private const float STATUS_VISIBLE_SECONDS = 2.5f;

        private const string HEADER_SUBTITLE = "Spend those tips";
        private const string HEADER_TITLE = "KurierShop";

        [Tooltip("Parent for shop rows. If null, a 'ShopItemsContainer' is created under this panel.")]
        [SerializeField] private RectTransform itemsContainer;

        [Tooltip("Text for purchase feedback. If null, one is created at the bottom of the panel.")]
        [SerializeField] private TMP_Text statusText;

        [SerializeField] private float rowHeight = 124f;

        private readonly List<ShopItemView> rows = new List<ShopItemView>();
        private PhoneUIStyle.Assets style;
        private float statusHideTime;

        private void Awake()
        {
            style = PhoneUIStyle.Resolve(this);
            PhoneUIStyle.SetPanelHeader(transform, HEADER_SUBTITLE, HEADER_TITLE);

            EnsureItemsContainer();
            EnsureStatusText();
        }

        private void OnEnable()
        {
            WalletManager.OnBalanceChanged += HandleBalanceChanged;
            UpgradeManager.OnUpgradesChanged += RefreshRows;

            BuildRowsIfNeeded();
            RefreshRows();
            SetStatus(string.Empty, PhoneUIStyle.TextPrimary);
        }

        private void OnDisable()
        {
            WalletManager.OnBalanceChanged -= HandleBalanceChanged;
            UpgradeManager.OnUpgradesChanged -= RefreshRows;
        }

        private void Update()
        {
            if (statusText != null && statusText.text.Length > 0 && Time.unscaledTime >= statusHideTime)
            {
                statusText.text = string.Empty;
            }
        }

        private void BuildRowsIfNeeded()
        {
            if (rows.Count > 0) return;

            var manager = UpgradeManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("<color=yellow>[ShopAppUI]</color> UpgradeManager.Instance is null. Shop is empty.");
                return;
            }

            foreach (var item in manager.Catalog)
            {
                rows.Add(ShopItemView.Create(itemsContainer, item, style, rowHeight, HandlePurchaseRequested));
            }
        }

        private void RefreshRows()
        {
            var manager = UpgradeManager.Instance;
            var wallet = WalletManager.Instance;
            if (manager == null) return;

            foreach (var row in rows)
            {
                var type = row.Item.UpgradeType;
                bool canAfford = wallet != null && wallet.CanAfford(row.Item.Price);
                row.Refresh(manager.IsOwned(type), manager.GetCharges(type), canAfford);
            }
        }

        private void HandleBalanceChanged(float _) => RefreshRows();

        private void HandlePurchaseRequested(CourierUpgradeItem item)
        {
            var manager = UpgradeManager.Instance;
            if (manager == null) return;

            switch (manager.TryPurchase(item))
            {
                case PurchaseResult.Success:
                    SetStatus($"Bought {item.Name}!", PhoneUIStyle.Positive);
                    break;
                case PurchaseResult.AlreadyOwned:
                    SetStatus("You already own this.", PhoneUIStyle.Indigo);
                    break;
                case PurchaseResult.InsufficientFunds:
                    SetStatus("Not enough cash. Go deliver something.", PhoneUIStyle.Negative);
                    break;
                default:
                    SetStatus("Shop is offline. Try again later.", PhoneUIStyle.Negative);
                    break;
            }
        }

        private void SetStatus(string message, Color color)
        {
            if (statusText == null) return;

            statusText.text = message;
            statusText.color = color;
            statusHideTime = Time.unscaledTime + STATUS_VISIBLE_SECONDS;
        }

        private void EnsureItemsContainer()
        {
            if (itemsContainer != null) return;

            itemsContainer = CreateStretchedChild("ShopItemsContainer");
            itemsContainer.offsetMin = new Vector2(CONTAINER_SIDE_OFFSET, CONTAINER_BOTTOM_OFFSET);
            itemsContainer.offsetMax = new Vector2(-CONTAINER_SIDE_OFFSET, -CONTAINER_TOP_OFFSET);

            var layout = itemsContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = ROW_SPACING;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private void EnsureStatusText()
        {
            if (statusText != null) return;

            var rect = CreateStretchedChild("ShopStatusText");
            rect.anchorMax = new Vector2(1f, 0f);
            rect.offsetMin = new Vector2(CONTAINER_SIDE_OFFSET, CONTAINER_BOTTOM_OFFSET - STATUS_HEIGHT);
            rect.offsetMax = new Vector2(-CONTAINER_SIDE_OFFSET, CONTAINER_BOTTOM_OFFSET);

            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (style.Font != null) text.font = style.Font;
            text.fontSize = STATUS_FONT_SIZE;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.text = string.Empty;
            statusText = text;
        }

        private RectTransform CreateStretchedChild(string childName)
        {
            var rect = new GameObject(childName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
