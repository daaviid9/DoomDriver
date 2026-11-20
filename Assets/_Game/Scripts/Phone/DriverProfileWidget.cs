using UnityEngine;
using TMPro;

namespace DoomDriver.Phone
{
    /// <summary>
    /// Displays dynamic wallet cash balance for the driver profile on the Home Screen.
    /// Subscribes to WalletManager.OnBalanceChanged for real-time updates.
    /// </summary>
    public class DriverProfileWidget : MonoBehaviour
    {
        [Header("UI Elements")]
        [Tooltip("Text displaying current wallet balance (e.g. '€ 27.50')")]
        [SerializeField] private TMP_Text walletBalanceText;

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
        /// Refreshes wallet display from WalletManager.
        /// </summary>
        public void Refresh()
        {
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
