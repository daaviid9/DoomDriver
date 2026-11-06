using System;
using UnityEngine;

/// <summary>
/// Central backend controller for delivery navigation, turn evaluations, and dynamic rerouting.
/// Coordinates between EnvironmentSpawner, TurnTrigger, and DashboardNavigationSource (Dev 5).
/// Fulfills Card 4.2 – Turn Detection & Recalculating.
/// </summary>
public class RouteController : MonoBehaviour
{
    public static RouteController Instance { get; private set; }

    [Header("Penalty & Rerouting Settings")]
    [Tooltip("Financial deduction per missed turn (e.g. 5.00€).")]
    [SerializeField] private float penaltyPerMissedTurn = 5f;

    [Tooltip("Number of extra straight road segments injected when recalculating (e.g. 3 chunks = ~134m).")]
    [SerializeField] private int recalculationChunks = 3;

    [Header("Route State")]
    [SerializeField] private int completedTurnsCount = 0;
    [SerializeField] private int missedTurnsCount = 0;
    [SerializeField] private string currentManeuverId = "";
    [SerializeField] private DashboardNavigationSource.Maneuver currentManeuverDirection = DashboardNavigationSource.Maneuver.Straight;

    private DashboardNavigationSource navigationSource;

    public int CompletedTurnsCount => completedTurnsCount;
    public int MissedTurnsCount => missedTurnsCount;
    public decimal TotalMissedTurnPenalty => (decimal)(missedTurnsCount * penaltyPerMissedTurn);
    public bool HasActiveManeuver => !string.IsNullOrEmpty(currentManeuverId);
    public DashboardNavigationSource.Maneuver CurrentManeuverDirection => currentManeuverDirection;

    // Events for decoupled observation (HUD, audio, stats)
    public event Action<int> OnCorrectTurn;
    public event Action<int> OnMissedTurn;
    public event Action OnRecalculating;

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
    }

    private void Start()
    {
        navigationSource = UnityEngine.Object.FindFirstObjectByType<DashboardNavigationSource>();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnOrderSelectionStarted += ResetRouteStats;
            GameManager.Instance.OnDeliveryStarted += ResetRouteStats;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnOrderSelectionStarted -= ResetRouteStats;
            GameManager.Instance.OnDeliveryStarted -= HandleDeliveryStarted;
        }

        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.DeliveryActive) return;

        UpdateUpcomingManeuverGPS();
    }

    /// <summary>
    /// Evaluates the outcome of a turn taken by the player at an intersection.
    /// Called by TurnTrigger.
    /// </summary>
    public void ReportTurnOutcome(TurnTrigger trigger, DashboardNavigationSource.Maneuver actual, DashboardNavigationSource.Maneuver expected, bool isCorrect)
    {
        if (navigationSource != null)
        {
            navigationSource.ReportTurnTaken(currentManeuverId, actual);
        }

        if (isCorrect)
        {
            completedTurnsCount++;
            Debug.Log($"<color=green>[RouteController] Correct turn executed!</color> (Total completed: {completedTurnsCount})");
            OnCorrectTurn?.Invoke(completedTurnsCount);

            // Clear current maneuver ID so the next intersection takes over
            currentManeuverId = "";
        }
        else
        {
            missedTurnsCount++;
            Debug.LogWarning($"<color=orange>[RouteController] Missed turn! Activating Recalculating.</color> (Total misses: {missedTurnsCount}, Penalty: -{TotalMissedTurnPenalty}€)");
            OnMissedTurn?.Invoke(missedTurnsCount);
            OnRecalculating?.Invoke();

            // Inject penalty road chunks to extend the route
            if (EnvironmentSpawner.Instance != null)
            {
                EnvironmentSpawner.Instance.AddRecalculationChunks(recalculationChunks);
            }

            currentManeuverId = "";
        }
    }

    private void UpdateUpcomingManeuverGPS()
    {
        if (navigationSource == null)
        {
            navigationSource = UnityEngine.Object.FindFirstObjectByType<DashboardNavigationSource>();
            if (navigationSource == null) return;
        }

        if (EnvironmentSpawner.Instance == null) return;

        // Find nearest active intersection ahead of vehicle
        RoadChunk nearestIntersection = null;
        var activeChunks = EnvironmentSpawner.Instance.ActiveChunks;
        for (int i = 0; i < activeChunks.Count; i++)
        {
            if (activeChunks[i] != null && activeChunks[i].TryGetComponent<RoadChunk>(out var chunk))
            {
                if (chunk.IsIntersection && chunk.GetDistanceToCenter() >= -chunk.TurnTriggerWindow)
                {
                    nearestIntersection = chunk;
                    break;
                }
            }
        }

        if (nearestIntersection != null)
        {
            float remainingMeters = Mathf.Max(0f, nearestIntersection.GetDistanceToCenter());

            currentManeuverDirection = nearestIntersection.ChunkType switch
            {
                RoadChunkType.TurnLeft => DashboardNavigationSource.Maneuver.Left,
                RoadChunkType.TurnRight => DashboardNavigationSource.Maneuver.Right,
                _ => DashboardNavigationSource.Maneuver.Straight
            };

            // Stable maneuver ID per unique intersection instance
            currentManeuverId = $"delivery-turn-{nearestIntersection.GetInstanceID()}";

            navigationSource.Publish(currentManeuverId, currentManeuverDirection, remainingMeters);
        }
        else
        {
            if (HasActiveManeuver)
            {
                currentManeuverId = "";
                navigationSource.ClearRoute();
            }
        }
    }

    public void ResetRouteStats()
    {
        completedTurnsCount = 0;
        missedTurnsCount = 0;
        currentManeuverId = "";

        if (navigationSource != null)
        {
            navigationSource.ClearRoute();
        }
    }

    private void HandleDeliveryStarted()
    {
        ResetRouteStats();
    }
}
