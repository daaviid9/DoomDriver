using UnityEngine;
using DoomDriver.Economy;

namespace DoomDriver.Economy
{
    /// <summary>
    /// Calculates the final DeliveryResult upon delivery completion and submits it to GameManager.
    /// Adheres to SoC: isolates financial payout, overtime bonus, damage, and missed-turn penalty calculations
    /// from DeliveryProgressTracker.
    /// </summary>
    public class DeliveryRewardCalculator : MonoBehaviour
    {
        [Header("Penalty Settings")]
        [Tooltip("Amount deducted per vehicle HP lost upon delivery.")]
        [SerializeField] private float damagePenaltyPerHp = 0.05f;

        [Header("Optional Scene Reference")]
        [SerializeField] private VehicleHealth vehicleHealth;

        private void Awake()
        {
            if (vehicleHealth == null)
            {
                vehicleHealth = FindFirstObjectByType<VehicleHealth>();
            }
        }

        private void OnEnable()
        {
            if (DeliveryProgressTracker.Instance != null)
            {
                DeliveryProgressTracker.Instance.OnTargetDistanceReached -= HandleTargetDistanceReached;
                DeliveryProgressTracker.Instance.OnTargetDistanceReached += HandleTargetDistanceReached;
            }
        }

        private void Start()
        {
            if (DeliveryProgressTracker.Instance != null)
            {
                DeliveryProgressTracker.Instance.OnTargetDistanceReached -= HandleTargetDistanceReached;
                DeliveryProgressTracker.Instance.OnTargetDistanceReached += HandleTargetDistanceReached;
            }
        }

        private void OnDisable()
        {
            if (DeliveryProgressTracker.Instance != null)
            {
                DeliveryProgressTracker.Instance.OnTargetDistanceReached -= HandleTargetDistanceReached;
            }
        }

        private void HandleTargetDistanceReached(DeliveryOrder order, bool isOvertime, float overtimePenalty, float elapsedSeconds)
        {
            if (vehicleHealth == null)
            {
                vehicleHealth = FindFirstObjectByType<VehicleHealth>();
            }

            int lostHp = vehicleHealth != null
                ? Mathf.Clamp(vehicleHealth.MaxHealth - vehicleHealth.CurrentHealth, 0, vehicleHealth.MaxHealth)
                : 0;

            decimal missedPenalty = RouteController.Instance != null ? RouteController.Instance.TotalMissedTurnPenalty : 0m;
            bool isQuotaMet = ShiftManager.Instance != null && ShiftManager.Instance.IsQuotaMet;
            int overtimeBonusPercent = ShiftManager.Instance != null ? ShiftManager.Instance.OvertimeBonusPercent : 0;

            DeliveryResult result = Calculate(
                order,
                isOvertime,
                overtimePenalty,
                elapsedSeconds,
                lostHp,
                damagePenaltyPerHp,
                missedPenalty,
                isQuotaMet,
                overtimeBonusPercent
            );

            Debug.Log($"<color=green>[DeliveryRewardCalculator]</color> Delivery Succeeded! Base: {result.BaseFee:F2}€, Tip: {result.TimeTip:F2}€, Damage: -{result.DamagePenalty:F2}€, Missed: -{result.MissedTurnPenalty:F2}€, Net: {result.NetEarnings:F2}€");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetDeliveryReward(result);
                GameManager.Instance.CompleteDelivery();
            }
        }

        /// <summary>
        /// Pure calculation method for delivery rewards and deductions (unit-testable).
        /// </summary>
        public static DeliveryResult Calculate(
            DeliveryOrder order,
            bool isOvertime,
            float overtimePenalty,
            float elapsedSeconds,
            int lostHp,
            float damagePenaltyPerHp,
            decimal missedTurnPenalty,
            bool isQuotaMet,
            int overtimeBonusPercent)
        {
            decimal basePayout = order != null ? (decimal)order.BasePayout : 15m;
            decimal maxTip = order != null ? (decimal)order.MaxTip : 5m;
            decimal timeTip = 0m;
            decimal penaltyDecimal = 0m;

            if (!isOvertime)
            {
                timeTip = maxTip;

                if (isQuotaMet && overtimeBonusPercent > 0)
                {
                    decimal bonusMultiplier = 1m + (overtimeBonusPercent / 100m);
                    timeTip = System.Math.Round(timeTip * bonusMultiplier, 2, System.MidpointRounding.AwayFromZero);
                }
            }
            else
            {
                penaltyDecimal = (decimal)overtimePenalty;
            }

            decimal adjustedBaseFee = System.Math.Max(0m, basePayout - penaltyDecimal);
            decimal damagePenalty = (decimal)(lostHp * damagePenaltyPerHp);

            return new DeliveryResult(
                baseFee: adjustedBaseFee,
                timeTip: timeTip,
                damagePenalty: damagePenalty,
                missedTurnPenalty: missedTurnPenalty,
                isPlaceholder: false,
                elapsedSeconds: elapsedSeconds,
                damageHp: lostHp
            );
        }
    }
}

