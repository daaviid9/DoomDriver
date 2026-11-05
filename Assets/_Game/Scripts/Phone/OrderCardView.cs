using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DoomDriver.Economy;

namespace DoomDriver.Phone
{
    /// <summary>
    /// Handles displaying a single DeliveryOrder on a UI card and forwarding the selection.
    /// </summary>
    public class OrderCardView : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TMP_Text restaurantText;
        [SerializeField] private TMP_Text customerText;
        [SerializeField] private TMP_Text distanceText;
        [SerializeField] private TMP_Text timeLimitText;
        [SerializeField] private TMP_Text payoutText;
        [SerializeField] private TMP_Text tipText;
        [SerializeField] private Button selectButton;

        private DeliveryOrder boundOrder;
        private int boundIndex;

        private void Awake()
        {
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

        /// <summary>
        /// Populates UI fields with order data.
        /// </summary>
        public void Setup(DeliveryOrder order, int index)
        {
            boundOrder = order;
            boundIndex = index;

            if (restaurantText != null) restaurantText.text = order.RestaurantName;
            if (customerText != null) customerText.text = order.CustomerName;
            if (distanceText != null) distanceText.text = $"{order.TotalDistance:F0} m";
            if (timeLimitText != null) timeLimitText.text = $"{order.TimeLimit:F0}s";
            if (payoutText != null) payoutText.text = $"{order.BasePayout:F0} €";
            if (tipText != null) tipText.text = $"+{order.MaxTip:F0} €";

            Debug.Log($"<color=green>[OrderCardView]</color> Setup [{index}]: {order.RestaurantName} ({order.Difficulty}) - {order.BasePayout:F0}€ payout, {order.TotalDistance:F0}m");
        }

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
