using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DoomDriver.Economy;

namespace DoomDriver.Phone
{
    /// <summary>
    /// Handles displaying a single DeliveryOrder on a UI card, applying difficulty color themes, and forwarding selection.
    /// </summary>
    public class OrderCardView : MonoBehaviour
    {
        [System.Serializable]
        public struct DifficultyTheme
        {
            [Tooltip("Primary accent color (button, top stripe, tip text)")]
            public Color primaryColor;

            [Tooltip("Lighter background accent color (e.g. restaurant icon square)")]
            public Color secondaryColor;

            public DifficultyTheme(Color primary, Color secondary)
            {
                primaryColor = primary;
                secondaryColor = secondary;
            }
        }

        [Header("UI Elements")]
        [SerializeField] private TMP_Text restaurantText;
        [SerializeField] private TMP_Text customerText;
        [SerializeField] private TMP_Text distanceText;
        [SerializeField] private TMP_Text timeLimitText;
        [SerializeField] private TMP_Text payoutText;
        [SerializeField] private TMP_Text tipText;
        [SerializeField] private Button selectButton;

        [Header("Difficulty Visual Elements")]
        [Tooltip("Top accent border/stripe of the card")]
        [SerializeField] private Image topStripe;

        [Tooltip("Restaurant icon image / background on the left")]
        [SerializeField] private Image restaurantImage;

        [Tooltip("Accept button background image. If null, selectButton's image is used.")]
        [SerializeField] private Image buttonImage;

        [Tooltip("Additional optional graphics (Image / TMP_Text) that should receive the primary accent color")]
        [SerializeField] private Graphic[] additionalPrimaryGraphics;

        [Tooltip("Additional optional graphics that should receive the secondary accent color")]
        [SerializeField] private Graphic[] additionalSecondaryGraphics;

        [Header("Difficulty Color Themes")]
        [SerializeField] private DifficultyTheme easyTheme = new DifficultyTheme(
            new Color(0.204f, 0.827f, 0.600f, 1f), // #34D399 (vibrant mint green)
            new Color(0.803f, 1.000f, 0.646f, 1f)  // #CDFFAB (light pastel green)
        );

        [SerializeField] private DifficultyTheme mediumTheme = new DifficultyTheme(
            new Color(0.961f, 0.620f, 0.043f, 1f), // #F59E0B (sunny amber/orange)
            new Color(0.992f, 0.902f, 0.541f, 1f)  // #FDE68A (light pastel amber)
        );

        [SerializeField] private DifficultyTheme hardTheme = new DifficultyTheme(
            new Color(0.957f, 0.247f, 0.369f, 1f), // #F43F5E (punchy coral red)
            new Color(0.996f, 0.804f, 0.827f, 1f)  // #FECDD3 (light pastel red/pink)
        );

        private DeliveryOrder boundOrder;
        private int boundIndex;

        private void Awake()
        {
            AutoResolveComponents();

            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(OnSelectClicked);
                selectButton.onClick.AddListener(OnSelectClicked);
            }
            else
            {
                Debug.LogWarning($"<color=yellow>[OrderCardView]</color> selectButton is null on '{gameObject.name}'!");
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            AutoResolveComponents();
        }
#endif

        /// <summary>
        /// Automatically resolves UI component references if not explicitly assigned in the Inspector.
        /// </summary>
        private void AutoResolveComponents()
        {
            if (topStripe == null)
            {
                var stripeTransform = transform.Find("TopStripe");
                if (stripeTransform != null)
                {
                    topStripe = stripeTransform.GetComponent<Image>();
                }
            }

            if (restaurantImage == null)
            {
                var images = GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    if (images[i].gameObject.name == "RestaurantImage")
                    {
                        restaurantImage = images[i];
                        break;
                    }
                }
            }

            if (buttonImage == null && selectButton != null)
            {
                buttonImage = selectButton.GetComponent<Image>();
            }
        }

        /// <summary>
        /// Populates UI fields with order data, formats time, and applies color themes by difficulty.
        /// </summary>
        public void Setup(DeliveryOrder order, int index)
        {
            boundOrder = order;
            boundIndex = index;

            if (restaurantText != null) restaurantText.text = order.RestaurantName;
            if (customerText != null) customerText.text = order.CustomerName;
            if (distanceText != null) distanceText.text = $"{order.TotalDistance:F0} m";
            if (timeLimitText != null) timeLimitText.text = FormatTime(order.TimeLimit);
            if (payoutText != null) payoutText.text = $"{order.BasePayout:F0} €";
            if (tipText != null) tipText.text = $"+{order.MaxTip:F0} €";

            ApplyDifficultyTheme(order.Difficulty);

            Debug.Log($"<color=green>[OrderCardView]</color> Setup [{index}]: {order.RestaurantName} ({order.Difficulty}) - {order.BasePayout:F0}€ payout, {order.TotalDistance:F0}m, limit: {FormatTime(order.TimeLimit)}");
        }

        /// <summary>
        /// Formats time in seconds: 60+ seconds displays as m:ss (e.g. 1:00, 1:08, 1:25), under 60 seconds as Xs (e.g. 45s).
        /// </summary>
        private static string FormatTime(float timeInSeconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(timeInSeconds));
            if (totalSeconds >= 60)
            {
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                return $"{minutes}:{seconds:D2}";
            }

            return $"{totalSeconds}s";
        }

        /// <summary>
        /// Applies the primary and secondary color palette according to difficulty.
        /// </summary>
        public void ApplyDifficultyTheme(DifficultyLevel difficulty)
        {
            DifficultyTheme theme = difficulty switch
            {
                DifficultyLevel.Easy => easyTheme,
                DifficultyLevel.Medium => mediumTheme,
                DifficultyLevel.Hard => hardTheme,
                _ => easyTheme
            };

            // 1. Top stripe
            if (topStripe != null)
            {
                topStripe.color = theme.primaryColor;
            }

            // 2. Button background
            Image btnImg = buttonImage != null ? buttonImage : (selectButton != null ? selectButton.image : null);
            if (btnImg != null)
            {
                btnImg.color = theme.primaryColor;
            }

            // 3. Tip text
            if (tipText != null)
            {
                tipText.color = theme.primaryColor;
            }

            // 4. Restaurant icon background (secondary accent)
            if (restaurantImage != null)
            {
                restaurantImage.color = theme.secondaryColor;
            }

            // 5. Additional user-configured graphics
            if (additionalPrimaryGraphics != null)
            {
                for (int i = 0; i < additionalPrimaryGraphics.Length; i++)
                {
                    if (additionalPrimaryGraphics[i] != null)
                    {
                        additionalPrimaryGraphics[i].color = theme.primaryColor;
                    }
                }
            }

            if (additionalSecondaryGraphics != null)
            {
                for (int i = 0; i < additionalSecondaryGraphics.Length; i++)
                {
                    if (additionalSecondaryGraphics[i] != null)
                    {
                        additionalSecondaryGraphics[i].color = theme.secondaryColor;
                    }
                }
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Preview Easy Theme")]
        private void PreviewEasyTheme()
        {
            AutoResolveComponents();
            ApplyDifficultyTheme(DifficultyLevel.Easy);
        }

        [ContextMenu("Preview Medium Theme")]
        private void PreviewMediumTheme()
        {
            AutoResolveComponents();
            ApplyDifficultyTheme(DifficultyLevel.Medium);
        }

        [ContextMenu("Preview Hard Theme")]
        private void PreviewHardTheme()
        {
            AutoResolveComponents();
            ApplyDifficultyTheme(DifficultyLevel.Hard);
        }
#endif

        private void OnSelectClicked()
        {
            Debug.Log($"<color=green>[OrderCardView]</color> Order card clicked! Index: {boundIndex}, Order: {boundOrder?.RestaurantName}");

            if (OrderManager.Instance != null)
            {
                OrderManager.Instance.SelectOrder(boundIndex);
            }
            else
            {
                Debug.LogError("<color=red>[OrderCardView]</color> OrderManager.Instance is null when clicking Accept Order!");
            }
        }
    }
}
