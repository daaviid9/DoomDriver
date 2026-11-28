using System;
using UnityEngine;
using DoomDriver.Navigation;

/// <summary>
/// Central backend controller for delivery navigation, turn evaluations, and GPS maneuver publishing.
/// Coordinates between EnvironmentSpawner, TurnTrigger, WorldTurnExecutor, and DashboardNavigationSource.
/// </summary>
public class RouteController : MonoBehaviour
{
    public static RouteController Instance { get; private set; }

    [Header("Penalty & Rerouting Settings")]
    [Tooltip("Financial deduction per missed turn (e.g. 5.00€).")]
    [SerializeField] private float penaltyPerMissedTurn = 5f;

    [Tooltip("Number of extra straight road segments injected when recalculating (e.g. 3 chunks = ~134m).")]
    [SerializeField] private int recalculationChunks = 3;

    [Tooltip("Duration in seconds the GPS displays the orange 'Recalculating...' status before resuming navigation cues.")]
    [SerializeField] private float recalculatingDuration = 2.5f;

    [Header("World Turn Animation")]
    [Tooltip("Default turn duration in seconds for Y-Fork (45-degree) turns.")]
    [SerializeField] private float forkTurnDuration = 0.95f;

    [Tooltip("Default turn duration in seconds for standard 90-degree intersection turns.")]
    [SerializeField] private float sharpTurnDuration = 1.1f;

    [SerializeField] private WorldTurnExecutor worldTurnExecutor;

    [Header("Route State")]
    [SerializeField] private int completedTurnsCount = 0;
    [SerializeField] private int missedTurnsCount = 0;
    [SerializeField] private string currentManeuverId = "";
    [SerializeField] private DashboardNavigationSource.Maneuver currentManeuverDirection = DashboardNavigationSource.Maneuver.Straight;

    private DashboardNavigationSource navigationSource;
    private float recalculatingTimer = 0f;

    public float ForkTurnDuration { get => forkTurnDuration; set => forkTurnDuration = Mathf.Max(0.05f, value); }
    public float SharpTurnDuration { get => sharpTurnDuration; set => sharpTurnDuration = Mathf.Max(0.05f, value); }
    public int CompletedTurnsCount => completedTurnsCount;
    public int MissedTurnsCount => missedTurnsCount;
    public decimal TotalMissedTurnPenalty => (decimal)(missedTurnsCount * penaltyPerMissedTurn);
    public bool HasActiveManeuver => !string.IsNullOrEmpty(currentManeuverId);
    public DashboardNavigationSource.Maneuver CurrentManeuverDirection => currentManeuverDirection;

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

        if (worldTurnExecutor == null)
        {
            worldTurnExecutor = GetComponent<WorldTurnExecutor>() ?? gameObject.AddComponent<WorldTurnExecutor>();
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
            GameManager.Instance.OnDeliveryStarted -= ResetRouteStats;
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
            currentManeuverId = "";
        }
        else
        {
            missedTurnsCount++;
            recalculatingTimer = recalculatingDuration;
            Debug.LogWarning($"<color=orange>[RouteController] Missed turn! Activating Recalculating.</color> (Total misses: {missedTurnsCount}, Penalty: -{TotalMissedTurnPenalty}€)");
            OnMissedTurn?.Invoke(missedTurnsCount);
            OnRecalculating?.Invoke();

            if (EnvironmentSpawner.Instance != null)
            {
                EnvironmentSpawner.Instance.AddRecalculationChunks(recalculationChunks);
            }

            if (navigationSource != null)
            {
                navigationSource.SetRecalculating();
            }

            currentManeuverId = "";
        }
    }

    /// <summary>
    /// Delegates the smooth world pivot turn transition to WorldTurnExecutor.
    /// </summary>
    public void ExecuteWorldTurn(RoadChunk intersectionChunk, DashboardNavigationSource.Maneuver direction)
    {
        if (intersectionChunk == null || worldTurnExecutor == null) return;
        worldTurnExecutor.ExecuteTurn(intersectionChunk, direction, forkTurnDuration, sharpTurnDuration);
    }

    private void UpdateUpcomingManeuverGPS()
    {
        if (recalculatingTimer > 0f)
        {
            recalculatingTimer -= Time.deltaTime;
            return;
        }

        if (navigationSource == null)
        {
            navigationSource = UnityEngine.Object.FindFirstObjectByType<DashboardNavigationSource>();
            if (navigationSource == null) return;
        }

        if (EnvironmentSpawner.Instance == null) return;

        RoadChunk nearestIntersection = null;
        var activeChunks = EnvironmentSpawner.Instance.ActiveChunks;
        for (int i = 0; i < activeChunks.Count; i++)
        {
            if (activeChunks[i] != null && activeChunks[i].TryGetComponent<RoadChunk>(out var chunk))
            {
                if (chunk.IsIntersection && chunk.GetDistanceToCenter() >= -chunk.TurnTriggerWindow)
                {
                    if (chunk.TryGetComponent<TurnTrigger>(out var trigger) && trigger.HasTriggered)
                    {
                        continue;
                    }

                    nearestIntersection = chunk;
                    break;
                }
            }
        }

        if (nearestIntersection != null)
        {
            float remainingMeters = Mathf.Max(0f, nearestIntersection.GetDistanceToCenter());

            if (nearestIntersection.ChunkType == RoadChunkType.YFork)
            {
                bool targetRight = nearestIntersection.TargetManeuver == DashboardNavigationSource.Maneuver.Right ||
                                   nearestIntersection.TargetManeuver == DashboardNavigationSource.Maneuver.ForkRight;
                currentManeuverDirection = targetRight
                    ? DashboardNavigationSource.Maneuver.ForkRight
                    : DashboardNavigationSource.Maneuver.ForkLeft;
            }
            else
            {
                currentManeuverDirection = nearestIntersection.TargetManeuver != DashboardNavigationSource.Maneuver.Straight
                    ? nearestIntersection.TargetManeuver
                    : nearestIntersection.ChunkType switch
                    {
                        RoadChunkType.TurnLeft => DashboardNavigationSource.Maneuver.Left,
                        RoadChunkType.TurnRight => DashboardNavigationSource.Maneuver.Right,
                        _ => DashboardNavigationSource.Maneuver.Straight
                    };
            }

            currentManeuverId = $"delivery-turn-{nearestIntersection.GetInstanceID()}";
            navigationSource.Publish(currentManeuverId, currentManeuverDirection, remainingMeters);
        }
        else if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DeliveryActive)
        {
            float carZ = VehicleMovement.Instance != null ? VehicleMovement.Instance.transform.position.z : 0f;
            float straightDistance;
            if (EnvironmentSpawner.Instance.TryGetQueuedTurnDistance(carZ, out float queuedTurnDist))
            {
                straightDistance = queuedTurnDist;
            }
            else if (DeliveryProgressTracker.Instance != null)
            {
                straightDistance = DeliveryProgressTracker.Instance.RemainingDistance;
            }
            else
            {
                straightDistance = 150f;
            }

            currentManeuverId = "delivery-straight-corridor";
            currentManeuverDirection = DashboardNavigationSource.Maneuver.Straight;
            navigationSource.Publish(currentManeuverId, currentManeuverDirection, straightDistance);
        }
        else if (HasActiveManeuver)
        {
            currentManeuverId = "";
            navigationSource.ClearRoute();
        }
    }

    public void ResetRouteStats()
    {
        completedTurnsCount = 0;
        missedTurnsCount = 0;
        currentManeuverId = "";
        recalculatingTimer = 0f;

        if (navigationSource != null)
        {
            navigationSource.ClearRoute();
        }
    }
}
