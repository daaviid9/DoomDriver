using UnityEngine;
using DoomDriver.Economy;

/// <summary>
/// Tracks delivery mission metrics (distance traveled, time elapsed, timer countdown, overtime penalties).
/// Coordinates route completion and failure conditions based on the active DeliveryOrder.
/// Follows Single Responsibility Principle (SRP) by keeping metric accumulation out of GameManager.
/// </summary>
public class DeliveryProgressTracker : MonoBehaviour
{
    public static DeliveryProgressTracker Instance { get; private set; }

    [Header("Mission Metrics")]
    [SerializeField] private float currentDistance = 0f;
    [SerializeField] private float targetDistance = 500f;
    [SerializeField] private float deliveryTimeElapsed = 0f;
    [SerializeField] private float timeLimit = 45f;
    [SerializeField] private float highscore = 0f;

    [Header("Overtime Settings")]
    [Tooltip("Maximum allowed seconds in overtime. Set to 0 to allow overtime until base payout is completely exhausted (e.g. -12€ on a 12€ order).")]
    [SerializeField] private float maxOvertimeDuration = 0f;

    [Tooltip("Amount deducted from base payout per second of overtime.")]
    [SerializeField] private float penaltyPerOvertimeSecond = 0.5f;

    [Tooltip("Amount deducted per vehicle HP lost upon delivery.")]
    [SerializeField] private float damagePenaltyPerHp = 0.05f;

    public float CurrentDistance => currentDistance;
    public float TargetDistance => targetDistance;
    public float RemainingDistance => Mathf.Max(0f, targetDistance - currentDistance);
    public float DeliveryTimeElapsed => deliveryTimeElapsed;
    public float TimeLimit => timeLimit;
    public float RemainingTime => Mathf.Max(0f, timeLimit - deliveryTimeElapsed);
    public bool IsOvertime => isMissionActive && deliveryTimeElapsed > timeLimit;
    public float OvertimeElapsed => IsOvertime ? (deliveryTimeElapsed - timeLimit) : 0f;
    public float CurrentOvertimePenalty => OvertimeElapsed * penaltyPerOvertimeSecond;
    public float MaxOvertimeDuration => maxOvertimeDuration;
    public float PenaltyPerOvertimeSecond => penaltyPerOvertimeSecond;
    public float Highscore => highscore;
    public DeliveryOrder CurrentOrder => currentOrder;

    /// <summary>
    /// Event fired whenever the accumulated distance changes. Passes current distance in meters.
    /// </summary>
    public event System.Action<float> OnDistanceUpdated;

    /// <summary>
    /// Event fired with current progress: (currentDistance, targetDistance).
    /// </summary>
    public event System.Action<float, float> OnDistanceProgressUpdated;

    /// <summary>
    /// Event fired whenever the mission elapsed time updates. Passes elapsed seconds.
    /// </summary>
    public event System.Action<float> OnTimeUpdated;

    /// <summary>
    /// Event fired every frame during active delivery:
    /// (remainingSeconds, isOvertime, overtimeSeconds, currentPenalty)
    /// </summary>
    public event System.Action<float, bool, float, float> OnTimerUpdated;

    private DeliveryOrder currentOrder;
    private bool isMissionActive = false;
    private const string HighscoreKey = "Highscore";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        highscore = PlayerPrefs.GetFloat(HighscoreKey, 0f);
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDeliveryStarted += HandleDeliveryStarted;
            GameManager.Instance.OnOrderSelectionStarted += HandleOrderSelectionStarted;
            GameManager.Instance.OnDeliveryFailed += CheckHighscore;
            GameManager.Instance.OnDeliveryCompleted += CheckHighscore;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDeliveryStarted -= HandleDeliveryStarted;
            GameManager.Instance.OnOrderSelectionStarted -= HandleOrderSelectionStarted;
            GameManager.Instance.OnDeliveryFailed -= CheckHighscore;
            GameManager.Instance.OnDeliveryCompleted -= CheckHighscore;
        }

        if (Instance == this) Instance = null;
    }

    private void HandleDeliveryStarted()
    {
        ResetProgress();
    }

    private void HandleOrderSelectionStarted()
    {
        isMissionActive = false;
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.DeliveryActive) return;
        if (PauseController.Instance != null && PauseController.Instance.IsPaused) return;
        if (!isMissionActive) return;

        deliveryTimeElapsed += Time.deltaTime;
        float forwardSpeed = WorldSpeedController.Instance != null ? WorldSpeedController.Instance.CurrentSpeed : 0f;
        currentDistance += forwardSpeed * Time.deltaTime;

        bool isOvertime = deliveryTimeElapsed > timeLimit;
        float overtimeSeconds = isOvertime ? (deliveryTimeElapsed - timeLimit) : 0f;
        float currentPenalty = overtimeSeconds * penaltyPerOvertimeSecond;
        float remainingTime = Mathf.Max(0f, timeLimit - deliveryTimeElapsed);

        OnTimeUpdated?.Invoke(deliveryTimeElapsed);
        OnDistanceUpdated?.Invoke(currentDistance);
        OnDistanceProgressUpdated?.Invoke(currentDistance, targetDistance);
        OnTimerUpdated?.Invoke(remainingTime, isOvertime, overtimeSeconds, currentPenalty);

        // Check if penalties consumed the full base payout (e.g. reached -12€ on a 12€ order)
        float maxAllowedPenalty = currentOrder != null ? currentOrder.BasePayout : 15f;
        if (isOvertime && currentPenalty >= maxAllowedPenalty)
        {
            Debug.LogWarning($"<color=red>[DeliveryProgressTracker]</color> Overtime penalties (-{currentPenalty:F2}€) exhausted full base payout ({maxAllowedPenalty:F2}€)! Order cancelled -> Delivery Failed.");
            isMissionActive = false;
            GameManager.Instance.FailDelivery();
            return;
        }

        // Optional safety duration check if maxOvertimeDuration is set > 0
        if (isOvertime && maxOvertimeDuration > 0f && overtimeSeconds >= maxOvertimeDuration)
        {
            Debug.LogWarning($"<color=red>[DeliveryProgressTracker]</color> Critical overtime exceeded ({overtimeSeconds:F1}s >= {maxOvertimeDuration:F1}s)! Order cancelled -> Delivery Failed.");
            isMissionActive = false;
            GameManager.Instance.FailDelivery();
            return;
        }

        // Check target distance completion
        if (currentDistance >= targetDistance)
        {
            Debug.Log($"<color=green>[DeliveryProgressTracker]</color> Target distance reached ({currentDistance:F1} / {targetDistance:F1}m)! Completing delivery.");
            isMissionActive = false;
            FinalizeDeliveryReward(isOvertime, overtimeSeconds, currentPenalty);
        }
    }

    private void FinalizeDeliveryReward(bool isOvertime, float overtimeSeconds, float currentPenalty)
    {
        decimal basePayout = currentOrder != null ? (decimal)currentOrder.BasePayout : 15m;
        decimal maxTip = currentOrder != null ? (decimal)currentOrder.MaxTip : 5m;
        decimal timeTip = 0m;
        decimal penaltyDecimal = 0m;

        if (!isOvertime)
        {
            timeTip = maxTip;
        }
        else
        {
            penaltyDecimal = (decimal)currentPenalty;
        }

        decimal adjustedBaseFee = System.Math.Max(0m, basePayout - penaltyDecimal);

        var vehicleHealth = FindFirstObjectByType<VehicleHealth>();
        int lostHp = (vehicleHealth != null) ? Mathf.Clamp(vehicleHealth.MaxHealth - vehicleHealth.CurrentHealth, 0, vehicleHealth.MaxHealth) : 0;
        decimal damagePenalty = (decimal)(lostHp * damagePenaltyPerHp);

        decimal missedPenalty = (RouteController.Instance != null) ? RouteController.Instance.TotalMissedTurnPenalty : 0m;

        var result = new DeliveryResult(
            baseFee: adjustedBaseFee,
            timeTip: timeTip,
            damagePenalty: damagePenalty,
            missedTurnPenalty: missedPenalty,
            isPlaceholder: false,
            elapsedSeconds: deliveryTimeElapsed,
            damageHp: lostHp
        );

        Debug.Log($"<color=green>[DeliveryProgressTracker]</color> Delivery Succeeded! Base: {adjustedBaseFee:F2}€ (-{penaltyDecimal:F2}€ overtime), Tip: {timeTip:F2}€, Damage: -{damagePenalty:F2}€, Net: {result.NetEarnings:F2}€");
        GameManager.Instance.SetDeliveryReward(result);
        GameManager.Instance.CompleteDelivery();
    }

    /// <summary>
    /// Resets progress counters when a new delivery begins.
    /// </summary>
    public void ResetProgress()
    {
        currentDistance = 0f;
        deliveryTimeElapsed = 0f;

        if (OrderManager.Instance != null && OrderManager.Instance.SelectedOrder != null)
        {
            currentOrder = OrderManager.Instance.SelectedOrder;
            targetDistance = currentOrder.TotalDistance;
            timeLimit = currentOrder.TimeLimit;
            Debug.Log($"<color=cyan>[DeliveryProgressTracker]</color> Configured from SelectedOrder: '{currentOrder.RestaurantName}' ({currentOrder.Difficulty}) - Target: {targetDistance:F0}m, Limit: {timeLimit:F0}s, Payout: {currentOrder.BasePayout:F0}€, Tip: {currentOrder.MaxTip:F0}€");
        }
        else
        {
            currentOrder = null;
            targetDistance = 500f;
            timeLimit = 45f;
            Debug.LogWarning("<color=yellow>[DeliveryProgressTracker]</color> OrderManager.SelectedOrder is null! Using fallback metrics: 500m / 45s.");
        }

        isMissionActive = true;
        OnDistanceUpdated?.Invoke(currentDistance);
        OnDistanceProgressUpdated?.Invoke(currentDistance, targetDistance);
        OnTimeUpdated?.Invoke(deliveryTimeElapsed);
        OnTimerUpdated?.Invoke(timeLimit, false, 0f, 0f);
    }

    /// <summary>
    /// Checks and saves highscore if the current run surpassed previous records.
    /// </summary>
    private void CheckHighscore()
    {
        isMissionActive = false;
        if (currentDistance > highscore)
        {
            highscore = currentDistance;
            PlayerPrefs.SetFloat(HighscoreKey, highscore);
            PlayerPrefs.Save();
        }
    }
}
