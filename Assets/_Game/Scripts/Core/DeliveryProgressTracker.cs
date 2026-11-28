using UnityEngine;
using DoomDriver.Economy;

/// <summary>
/// Tracks delivery mission metrics (distance traveled, time elapsed, timer countdown, overtime).
/// Adheres to SoC: does NOT calculate financial rewards or penalties (see DeliveryRewardCalculator).
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
    [Tooltip("Maximum allowed seconds in overtime. Set to 0 to allow overtime until base payout is completely exhausted.")]
    [SerializeField] private float maxOvertimeDuration = 0f;

    [Tooltip("Amount deducted from base payout per second of overtime.")]
    [SerializeField] private float penaltyPerOvertimeSecond = 0.5f;

    [Tooltip("Extra route distance (in meters) added when the player misses a turn.")]
    [SerializeField] private float missedTurnExtraDistance = 150f;

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

    public event System.Action<float> OnDistanceUpdated;
    public event System.Action<float, float> OnDistanceProgressUpdated;
    public event System.Action<float> OnTimeUpdated;
    public event System.Action<float, bool, float, float> OnTimerUpdated;

    /// <summary>
    /// Fired when the vehicle reaches the target delivery distance:
    /// (order, isOvertime, overtimePenalty, deliveryTimeElapsed)
    /// </summary>
    public event System.Action<DeliveryOrder, bool, float, float> OnTargetDistanceReached;

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

        if (RouteController.Instance != null)
        {
            RouteController.Instance.OnMissedTurn += HandleMissedTurn;
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

        if (RouteController.Instance != null)
        {
            RouteController.Instance.OnMissedTurn -= HandleMissedTurn;
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

        float maxAllowedPenalty = currentOrder != null ? currentOrder.BasePayout : 15f;
        if (isOvertime && currentPenalty >= maxAllowedPenalty)
        {
            Debug.LogWarning($"<color=red>[DeliveryProgressTracker]</color> Overtime penalties (-{currentPenalty:F2}€) exhausted full base payout ({maxAllowedPenalty:F2}€)! Order cancelled -> Delivery Failed.");
            isMissionActive = false;
            GameManager.Instance.FailDelivery(FailureReason.TimeExpired);
            return;
        }

        if (isOvertime && maxOvertimeDuration > 0f && overtimeSeconds >= maxOvertimeDuration)
        {
            Debug.LogWarning($"<color=red>[DeliveryProgressTracker]</color> Critical overtime exceeded ({overtimeSeconds:F1}s >= {maxOvertimeDuration:F1}s)! Order cancelled -> Delivery Failed.");
            isMissionActive = false;
            GameManager.Instance.FailDelivery(FailureReason.TimeExpired);
            return;
        }

        if (currentDistance >= targetDistance)
        {
            Debug.Log($"<color=green>[DeliveryProgressTracker]</color> Target distance reached ({currentDistance:F1} / {targetDistance:F1}m)! Completing delivery.");
            isMissionActive = false;
            OnTargetDistanceReached?.Invoke(currentOrder, isOvertime, currentPenalty, deliveryTimeElapsed);
        }
    }

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

    public void ExtendTargetDistance(float extraMeters)
    {
        if (!isMissionActive || extraMeters <= 0f) return;

        targetDistance += extraMeters;
        Debug.Log($"<color=orange>[DeliveryProgressTracker]</color> Route extended by +{extraMeters:F0}m -> New target: {targetDistance:F0}m");
        OnDistanceUpdated?.Invoke(currentDistance);
        OnDistanceProgressUpdated?.Invoke(currentDistance, targetDistance);
    }

    private void HandleMissedTurn(int _)
    {
        ExtendTargetDistance(missedTurnExtraDistance);
    }

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
