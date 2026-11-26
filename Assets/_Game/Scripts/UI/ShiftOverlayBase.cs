using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DoomDriver.Core;
using DoomDriver.Phone;

namespace DoomDriver.UI
{
    /// <summary>
    /// Shared base for full-screen phone overlays tied to a GameState (DayBriefing, DaySummary).
    /// Builds its layout in code (same approach as ShopAppUI), shows itself while GameManager is in
    /// VisibleState, and takes over PhoneInputNavigator so Enter/Space presses its single action button.
    /// Presentation only: day, quota, bills and transitions live in ShiftManager / GameManager.
    /// Visuals follow PhoneUIStyle (cream app screen, white cards, Fredoka, mint action button) so the
    /// overlays look like the hand-made Courier app.
    /// </summary>
    public abstract class ShiftOverlayBase : MonoBehaviour
    {
        protected const float TITLE_FONT_SIZE = PhoneUIStyle.HEADER_SIZE;
        protected const float HEADER_FONT_SIZE = PhoneUIStyle.SUBTITLE_SIZE;
        protected const float BODY_FONT_SIZE = PhoneUIStyle.BODY_SIZE;
        protected const float SMALL_FONT_SIZE = PhoneUIStyle.SMALL_SIZE;
        protected const float ROW_FONT_SIZE = 18f;
        private const float BUTTON_HEIGHT = 72f;
        private const float STATUS_BAR_HEIGHT = 40f;
        private const int CONTENT_PADDING = 22;
        private const float CONTENT_SPACING = 14f;
        private const int CARD_PADDING = 18;
        private const float CARD_SPACING = 8f;

        protected static readonly Color TextColor = PhoneUIStyle.TextPrimary;
        protected static readonly Color AccentColor = PhoneUIStyle.Subtitle;
        protected static readonly Color HighlightColor = PhoneUIStyle.Indigo;
        protected static readonly Color MutedColor = PhoneUIStyle.TextMuted;
        protected static readonly Color PositiveColor = PhoneUIStyle.Positive;
        protected static readonly Color NegativeColor = PhoneUIStyle.Negative;
        protected static readonly Color InfoCardColor = PhoneUIStyle.PillLight;
        private static readonly Color DividerColor = new Color(0.227f, 0.227f, 0.227f, 0.15f);

        private GameObject overlayRoot;
        private PhoneActionButton actionButton;
        private PhoneInputNavigator navigator;
        private PhoneOSController phoneOS;
        private GameManager gameManager;
        private PhoneUIStyle.Assets style;
        private bool isVisible;

        protected abstract GameState VisibleState { get; }
        protected abstract string ObjectName { get; }
        protected abstract string ButtonLabel { get; }
        protected abstract void BuildContent(RectTransform content);
        protected abstract void Refresh(ShiftManager shiftManager);
        protected abstract void HandleButtonPressed(ShiftManager shiftManager);

        protected PhoneOSController PhoneOS => phoneOS;

        protected virtual void Awake()
        {
            phoneOS = GetComponentInParent<PhoneOSController>();
            navigator = ResolveNavigator();

            style = PhoneUIStyle.Resolve(this);

            BuildLayout();
            overlayRoot.SetActive(false);
        }

        protected virtual void Start()
        {
            gameManager = GameManager.Instance;
            if (gameManager == null || ShiftManager.Instance == null)
            {
                Debug.LogWarning($"[{GetType().Name}] GameManager or ShiftManager missing: overlay stays hidden.");
                return;
            }

            gameManager.OnGameStateChanged += HandleGameStateChanged;
            if (phoneOS != null) phoneOS.OnScreenChanged += HandleScreenChanged;
            HandleGameStateChanged(gameManager.CurrentState);
        }

        protected virtual void OnDestroy()
        {
            if (gameManager != null) gameManager.OnGameStateChanged -= HandleGameStateChanged;
            if (phoneOS != null) phoneOS.OnScreenChanged -= HandleScreenChanged;
        }

        private PhoneInputNavigator ResolveNavigator()
        {
            if (phoneOS != null)
            {
                var local = phoneOS.GetComponent<PhoneInputNavigator>() ?? phoneOS.GetComponentInChildren<PhoneInputNavigator>(true);
                if (local != null) return local;
            }

            return FindFirstObjectByType<PhoneInputNavigator>();
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (state == VisibleState) Show();
            else Hide();
        }

        private void Show()
        {
            var shift = ShiftManager.Instance;
            if (shift == null) return;

            Refresh(shift);
            transform.SetAsLastSibling();
            overlayRoot.transform.SetAsLastSibling();
            overlayRoot.SetActive(true);
            isVisible = true;
            ClaimNavigator();
        }

        private void Hide()
        {
            isVisible = false;
            overlayRoot.SetActive(false);
        }

        /// <summary>
        /// PhoneOSController re-registers its own items whenever it switches screens (e.g. Home on lobby entry),
        /// so reclaim keyboard focus while this overlay covers the phone.
        /// </summary>
        private void HandleScreenChanged(PhoneScreen screen)
        {
            if (isVisible) ClaimNavigator();
        }

        private void ClaimNavigator()
        {
            if (navigator == null) return;

            navigator.SetNavigableItems(new IPhoneNavigable[] { actionButton }, 0);
            navigator.SetNavigationActive(true);
        }

        private void OnActionPressed()
        {
            var shift = ShiftManager.Instance;
            if (!isVisible || shift == null) return;
            HandleButtonPressed(shift);
        }

        private void BuildLayout()
        {
            var rootRect = CreateRect(ObjectName, transform);
            Stretch(rootRect);
            rootRect.offsetMax = new Vector2(0f, -STATUS_BAR_HEIGHT);
            overlayRoot = rootRect.gameObject;

            // Same cream, rounded screen as the phone itself; blocks clicks on the home screen underneath.
            var background = PhoneUIStyle.AddRoundedImage(overlayRoot, style.LargeRadius, PhoneUIStyle.Background);
            background.raycastTarget = true;

            var column = overlayRoot.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(CONTENT_PADDING, CONTENT_PADDING, CONTENT_PADDING, CONTENT_PADDING);
            column.spacing = CONTENT_SPACING;
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            BuildContent(rootRect);

            CreateRect("Spacer", rootRect).gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            actionButton = CreateActionButton(rootRect);
        }

        private PhoneActionButton CreateActionButton(RectTransform parent)
        {
            // Mint call-to-action like the order card's "Accept Order" button.
            var buttonRect = CreateRect("ActionButton", parent);
            PhoneUIStyle.AddRoundedImage(buttonRect.gameObject, style.SmallRadius, PhoneUIStyle.ActionButton);
            buttonRect.gameObject.AddComponent<LayoutElement>().preferredHeight = BUTTON_HEIGHT;

            var label = CreateText(buttonRect, PhoneUIStyle.BUTTON_TEXT_SIZE, FontStyles.Bold, TextAlignmentOptions.Center, PhoneUIStyle.IndigoDark);
            label.text = ButtonLabel;
            Stretch((RectTransform)label.transform);

            var button = buttonRect.gameObject.AddComponent<PhoneActionButton>();
            button.Initialize(OnActionPressed);
            return button;
        }

        protected TMP_Text CreateText(Transform parent, float size, FontStyles style, TextAlignmentOptions alignment, Color color)
        {
            return PhoneUIStyle.CreateText(parent, this.style, size, style, alignment, color);
        }

        /// <summary>
        /// White rounded card (like OrderCard) that stacks its children vertically.
        /// Pass InfoCardColor for the light-blue "message" variant.
        /// </summary>
        protected RectTransform CreateCard(Transform parent, Color? fill = null)
        {
            var card = CreateRect("Card", parent);
            if (fill.HasValue) PhoneUIStyle.AddRoundedImage(card.gameObject, style.LargeRadius, fill.Value);
            else PhoneUIStyle.AddCard(card.gameObject, style);

            var column = card.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(CARD_PADDING, CARD_PADDING, CARD_PADDING, CARD_PADDING);
            column.spacing = CARD_SPACING;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            return card;
        }

        /// <summary>
        /// Small salmon subtitle + big title, matching the Courier app's DeliveriesHeaderPanel.
        /// </summary>
        protected (TMP_Text subtitle, TMP_Text title) CreateHeader(Transform parent)
        {
            var subtitle = CreateText(parent, HEADER_FONT_SIZE, FontStyles.Normal, TextAlignmentOptions.Left, AccentColor);
            var title = CreateText(parent, TITLE_FONT_SIZE, FontStyles.Normal, TextAlignmentOptions.Left, TextColor);
            return (subtitle, title);
        }

        /// <summary>
        /// Creates a "label ........ value" row and returns the value text.
        /// </summary>
        protected TMP_Text CreateRow(Transform parent, string label)
        {
            var rowRect = CreateRect($"Row_{label}", parent);
            var row = rowRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            var labelText = CreateText(rowRect, ROW_FONT_SIZE, FontStyles.Normal, TextAlignmentOptions.Left, TextColor);
            labelText.text = label;
            labelText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            return CreateText(rowRect, ROW_FONT_SIZE, FontStyles.Bold, TextAlignmentOptions.Right, TextColor);
        }

        protected void CreateDivider(Transform parent)
        {
            var divider = CreateRect("Divider", parent).gameObject;
            divider.AddComponent<Image>().color = DividerColor;
            divider.AddComponent<LayoutElement>().preferredHeight = 2f;
        }

        protected static string FormatMoney(decimal amount)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.00} €", amount);
        }

        protected static string FormatSigned(decimal amount)
        {
            string sign = amount > 0m ? "+" : amount < 0m ? "-" : string.Empty;
            return sign + FormatMoney(System.Math.Abs(amount));
        }

        protected static RectTransform CreateRect(string name, Transform parent) => PhoneUIStyle.CreateRect(name, parent);

        private static void Stretch(RectTransform rect) => PhoneUIStyle.Stretch(rect);
    }
}
