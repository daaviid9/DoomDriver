using System;
using UnityEngine;

namespace DoomDriver.Economy
{
    /// <summary>
    /// Manages penalty calculations and wallet deduction when a delivery fails.
    /// Follows Single Responsibility Principle (SRP): UI scripts never touch money or configure game fees.
    /// </summary>
    public class DeliveryPenaltyManager : MonoBehaviour
    {
        private static DeliveryPenaltyManager _instance;
        public static DeliveryPenaltyManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<DeliveryPenaltyManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("DeliveryPenaltyManager");
                        _instance = go.AddComponent<DeliveryPenaltyManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Vehicle Crash Penalties (EUR)")]
        [Tooltip("Flatbed tow truck fee")]
        [SerializeField] private float crashTowFee = 7.00f;

        [Tooltip("Fleet insurance deductible for repair")]
        [SerializeField] private float crashRepairDeductible = 5.00f;

        [Tooltip("Penalty for cancelled food order")]
        [SerializeField] private float crashCancelledOrderFee = 3.00f;

        [Header("Timeout / Delay Penalties (EUR)")]
        [Tooltip("Penalty charged by platform for unfulfilled delivery order")]
        [SerializeField] private float timeoutUnfulfilledOrderFee = 3.00f;

        [Tooltip("Compensation refunded to customer for excessive delay")]
        [SerializeField] private float timeoutCustomerDelayFee = 2.00f;

        [Tooltip("Courier platform service cancellation fee")]
        [SerializeField] private float timeoutPlatformCancellationFee = 1.00f;

        /// <summary>
        /// Raised when penalties are settled and deducted from the wallet.
        /// Passes the immutable result DTO to UI or analytics.
        /// </summary>
        public event Action<DeliveryFailureResult> OnPenaltySettled;

        public DeliveryFailureResult LatestResult { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Single entry point to settle penalties for a failure reason. GameManager.FailDelivery calls it once
        /// before raising OnDeliveryFailed, so do not also subscribe to OnDeliveryFailedWithReason:
        /// that charged the wallet twice per failed delivery.
        /// </summary>
        public void ProcessFailure(FailureReason reason)
        {
            SettleFailure(reason);
        }

        /// <summary>
        /// Calculates itemized penalties based on failure reason, deducts total from WalletManager,
        /// and broadcasts the settled result.
        /// </summary>
        private void SettleFailure(FailureReason reason)
        {
            float fee1 = 0f;
            float fee2 = 0f;
            float fee3 = 0f;

            switch (reason)
            {
                case FailureReason.VehicleCrash:
                    fee1 = crashTowFee;
                    fee2 = crashRepairDeductible;
                    fee3 = crashCancelledOrderFee;
                    break;

                case FailureReason.TimeExpired:
                    fee1 = timeoutUnfulfilledOrderFee;
                    fee2 = timeoutCustomerDelayFee;
                    fee3 = timeoutPlatformCancellationFee;
                    break;
            }

            LatestResult = new DeliveryFailureResult(reason, fee1, fee2, fee3);

            if (WalletManager.Instance != null && LatestResult.TotalPenalty > 0f)
            {
                WalletManager.Instance.DeductFunds(LatestResult.TotalPenalty);
                Debug.Log($"<color=orange>[DeliveryPenaltyManager]</color> Deducted {reason} penalty of {LatestResult.TotalPenalty:F2}€ from wallet.");
            }

            OnPenaltySettled?.Invoke(LatestResult);
        }
    }
}

