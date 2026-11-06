using UnityEngine;

/// <summary>
/// Attached to intersection road chunks.
/// Detects when the vehicle passes the intersection center (Z=19 by default)
/// and evaluates whether the player successfully followed the GPS navigation cue.
/// Fulfills Card 4.2 – Turn Detection & Recalculating.
/// </summary>
[RequireComponent(typeof(RoadChunk))]
public class TurnTrigger : MonoBehaviour
{
    private RoadChunk roadChunk;
    private bool hasTriggered = false;
    private bool isInEvaluationZone = false;
    private bool hasClearedObstacles = false;
    private DashboardNavigationSource.Maneuver recordedManeuver = DashboardNavigationSource.Maneuver.Straight;

    [Header("Evaluation Settings")]
    [Tooltip("Lateral X boundary threshold defining a turn to the left (< -1.5m) or right (> 1.5m).")]
    [SerializeField] private float lateralTurnThreshold = 1.5f;

    [Tooltip("Minimum steering analog input to register an intentional turn intent.")]
    [SerializeField] private float steeringInputThreshold = 0.3f;

    public bool HasTriggered => hasTriggered;
    public RoadChunk Chunk => roadChunk;

    private void Awake()
    {
        roadChunk = GetComponent<RoadChunk>();
    }

    private void Update()
    {
        if (hasTriggered) return;
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.DeliveryActive) return;

        float carZ = VehicleMovement.Instance != null ? VehicleMovement.Instance.transform.position.z : 0f;
        float distanceToCenter = roadChunk.GetDistanceToCenter(carZ);

        // Keep intersection corridor completely clear of traffic obstacles
        if (!hasClearedObstacles && distanceToCenter <= 50f)
        {
            hasClearedObstacles = true;
            ObstacleSpawner.ClearObstaclesInZRange(transform.position.z - 10f, transform.position.z + roadChunk.ChunkLength + 10f);
        }

        // 1. Check if vehicle is in the approach / evaluation window
        float approachWindow = Mathf.Max(roadChunk.TurnTriggerWindow, 12f);
        if (distanceToCenter <= approachWindow && distanceToCenter >= -roadChunk.TurnTriggerWindow)
        {
            isInEvaluationZone = true;
            SamplePlayerIntent();
        }

        // 2. When vehicle has reached or crossed the intersection center, evaluate final turn outcome
        if (isInEvaluationZone && distanceToCenter <= 0f)
        {
            EvaluateTurn();
        }
    }

    private void SamplePlayerIntent()
    {
        float carX = 0f;
        float steerX = 0f;

        if (VehicleMovement.Instance != null)
        {
            carX = VehicleMovement.Instance.transform.position.x;
            steerX = VehicleMovement.Instance.MoveInput.x;
        }

        // Determine player's active lane / steering intent
        if (carX < -lateralTurnThreshold || steerX < -steeringInputThreshold)
        {
            recordedManeuver = DashboardNavigationSource.Maneuver.Left;
        }
        else if (carX > lateralTurnThreshold || steerX > steeringInputThreshold)
        {
            recordedManeuver = DashboardNavigationSource.Maneuver.Right;
        }
        else
        {
            recordedManeuver = DashboardNavigationSource.Maneuver.Straight;
        }
    }

    private void EvaluateTurn()
    {
        hasTriggered = true;
        isInEvaluationZone = false;

        // Determine expected maneuver for this chunk
        DashboardNavigationSource.Maneuver expectedManeuver = GetExpectedManeuver();

        bool isCorrect = (recordedManeuver == expectedManeuver);

        Debug.Log($"[TurnTrigger] Intersection at Z={transform.position.z + roadChunk.IntersectionCenterZ:F1}: " +
                  $"Player took '{recordedManeuver}', Expected '{expectedManeuver}'. Correct: {isCorrect}");

        if (RouteController.Instance != null)
        {
            RouteController.Instance.ReportTurnOutcome(this, recordedManeuver, expectedManeuver, isCorrect);
        }

        if (isCorrect && VehicleMovement.Instance != null)
        {
            VehicleMovement.Instance.EaseToCenter(1.2f);
        }
    }

    private DashboardNavigationSource.Maneuver GetExpectedManeuver()
    {
        if (RouteController.Instance != null && RouteController.Instance.HasActiveManeuver)
        {
            return RouteController.Instance.CurrentManeuverDirection;
        }

        // Fallback directly to chunk type
        return roadChunk.ChunkType switch
        {
            RoadChunkType.TurnLeft => DashboardNavigationSource.Maneuver.Left,
            RoadChunkType.TurnRight => DashboardNavigationSource.Maneuver.Right,
            _ => DashboardNavigationSource.Maneuver.Straight
        };
    }
}

