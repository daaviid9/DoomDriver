using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DoomDriver.Phone
{
    /// <summary>
    /// Reusable top bar controller for sub-applications (Courier App, Shop, Bills).
    /// Provides back navigation to the Home Screen and dynamic wallet balance display.
    /// </summary>
    public class AppTopBar : MonoBehaviour
    {
        [Header("Back Navigation")]
        [Tooltip("Button that navigates back to Home Screen")]
        [SerializeField] private Button backButton;

        [Header("Wallet Display")]
        [Tooltip("Text displaying current wallet balance (e.g. '€ 27.50')")]
        [SerializeField] private TMP_Text walletBalanceText;

#if UNITY_EDITOR
        private void OnValidate()
        {
            AutoResolveReferences();
        }
#endif

        private void Awake()
        {
            AutoResolveReferences();

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(HandleBackClicked);
                backButton.onClick.AddListener(HandleBackClicked);
            }
        }

        private void OnEnable()
        {
            WalletManager.OnBalanceChanged += HandleBalanceChanged;
            RefreshWallet();
        }

        private void OnDisable()
        {
            WalletManager.OnBalanceChanged -= HandleBalanceChanged;
        }

        private void Start()
        {
            RefreshWallet();
        }

        private void AutoResolveReferences()
        {
            if (backButton == null)
            {
                backButton = GetComponentInChildren<Button>();
            }

            if (walletBalanceText == null)
            {
                var found = transform.Find("MoneyText") ?? transform.Find("WalletText") ?? transform.Find("BalanceText");
                if (found != null)
                {
                    walletBalanceText = found.GetComponent<TMP_Text>();
                }
                else
                {
                    walletBalanceText = GetComponentInChildren<TMP_Text>();
                }
            }
        }

        /// <summary>
        /// Manually refreshes the displayed wallet balance from WalletManager.
        /// </summary>
        public void RefreshWallet()
        {
            float current = WalletManager.Instance != null ? WalletManager.Instance.Balance : 0f;
            UpdateWalletText(current);
        }

        private void HandleBalanceChanged(float newBalance)
        {
            UpdateWalletText(newBalance);
        }

        private void UpdateWalletText(float amount)
        {
            if (walletBalanceText != null)
            {
                walletBalanceText.text = $"€ {amount:F2}";
            }
        }

        private void HandleBackClicked()
        {
            if (PhoneOSController.Instance != null)
            {
                PhoneOSController.Instance.ReturnToHomeScreen();
            }
            else
            {
                Debug.LogWarning("<color=yellow>[AppTopBar]</color> PhoneOSController.Instance is null! Cannot return to Home.");
            }
        }
    }
}

