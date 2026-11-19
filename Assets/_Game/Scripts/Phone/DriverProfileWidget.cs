using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DoomDriver.Phone
{
    /// <summary>
    /// Displays the driver's avatar, rating, and dynamic wallet cash balance.
    /// Can be used as a reusable widget on the Home Screen, Courier App header, Shop, and Bills.
    /// Subscribes to WalletManager.OnBalanceChanged for real-time updates.
    /// </summary>
    public class DriverProfileWidget : MonoBehaviour
    {
        [Header("UI Elements")]
        [Tooltip("Text displaying current wallet balance (e.g. '€ 27.50')")]
        [SerializeField] private TMP_Text walletBalanceText;

        [Tooltip("Text displaying driver rating (e.g. '★ 4.9')")]
        [SerializeField] private TMP_Text ratingText;

        [Tooltip("Avatar image of the courier driver")]
        [SerializeField] private Image avatarImage;

        [Header("Default Values")]
        [SerializeField] private string defaultRating = "★ 4.9";

        private void OnEnable()
        {
            WalletManager.OnBalanceChanged += HandleBalanceChanged;
            Refresh();
        }

        private void OnDisable()
        {
            WalletManager.OnBalanceChanged -= HandleBalanceChanged;
        }

        private void Start()
        {
            Refresh();
        }

        /// <summary>
        /// Refreshes all profile elements from WalletManager.
        /// </summary>
        public void Refresh()
        {
            if (ratingText != null && string.IsNullOrEmpty(ratingText.text))
            {
                ratingText.text = defaultRating;
            }

            float currentBalance = WalletManager.Instance != null ? WalletManager.Instance.Balance : 0f;
            UpdateBalanceText(currentBalance);
        }

        private void HandleBalanceChanged(float newBalance)
        {
            UpdateBalanceText(newBalance);
        }

        private void UpdateBalanceText(float amount)
        {
            if (walletBalanceText != null)
            {
                walletBalanceText.text = $"€ {amount:F2}";
            }
        }
    }
}

