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
    private DashboardNavigationSource.Maneuver latchedManeuver = DashboardNavigationSource.Maneuver.Straight;

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
        float approachWindow = Mathf.Max(roadChunk.TurnTriggerWindow, 16f);
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
            latchedManeuver = DashboardNavigationSource.Maneuver.Left;
        }
        else if (carX > lateralTurnThreshold || steerX > steeringInputThreshold)
        {
            recordedManeuver = DashboardNavigationSource.Maneuver.Right;
            latchedManeuver = DashboardNavigationSource.Maneuver.Right;
        }
        else
        {
            // Center zone (|carX| <= lateralTurnThreshold)
            if (latchedManeuver == DashboardNavigationSource.Maneuver.Straight)
            {
                recordedManeuver = DashboardNavigationSource.Maneuver.Straight;
            }
            else
            {
                // If vehicle clearly moved toward left branch (carX < -0.6m) or right branch (carX > 0.6m),
                // retain the latched maneuver intent so player isn't penalized if they release steer input just before apex
                if (carX < -0.6f && latchedManeuver == DashboardNavigationSource.Maneuver.Left)
                {
                    recordedManeuver = DashboardNavigationSource.Maneuver.Left;
                }
                else if (carX > 0.6f && latchedManeuver == DashboardNavigationSource.Maneuver.Right)
                {
                    recordedManeuver = DashboardNavigationSource.Maneuver.Right;
                }
                else
                {
                    recordedManeuver = DashboardNavigationSource.Maneuver.Straight;
                }
            }
        }
    }

    private void EvaluateTurn()
    {
        hasTriggered = true;
        isInEvaluationZone = false;

        // Fatal crash check on Y-Fork:
        // A Y-fork has no straight continuation path. Going straight means crashing into the central divider/wedge.
        // The player only survives if they steer Left or Right into one of the fork branches.
        if (roadChunk != null && roadChunk.IsFork && recordedManeuver == DashboardNavigationSource.Maneuver.Straight)
        {
            TriggerForkCentralCrash();
            return;
        }

        // Determine expected maneuver for this chunk
        DashboardNavigationSource.Maneuver expectedManeuver = GetExpectedManeuver();

        // In free drive / debug mode when no specific maneuver is required, both Left and Right are valid
        bool isFreeDrive = (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.DeliveryActive);
        bool isCorrect = isFreeDrive
            ? (recordedManeuver == DashboardNavigationSource.Maneuver.Left || recordedManeuver == DashboardNavigationSource.Maneuver.Right)
            : (recordedManeuver == expectedManeuver);

        Debug.Log($"[TurnTrigger] Intersection at Z={transform.position.z + roadChunk.IntersectionCenterZ:F1}: " +
                  $"Player took '{recordedManeuver}', Expected '{expectedManeuver}'. Correct: {isCorrect}");

        if (RouteController.Instance != null)
        {
            RouteController.Instance.ReportTurnOutcome(this, recordedManeuver, expectedManeuver, isCorrect);
        }

        if (isCorrect)
        {
            if (recordedManeuver == DashboardNavigationSource.Maneuver.Left || recordedManeuver == DashboardNavigationSource.Maneuver.Right)
            {
                if (RouteController.Instance != null)
                {
                    RouteController.Instance.ExecuteWorldTurn(roadChunk, recordedManeuver);
                }
            }
            else if (VehicleMovement.Instance != null)
            {
                VehicleMovement.Instance.EaseToCenter(1.2f);
            }
        }
        else
        {
            // If the player took the wrong fork branch at a YFork (e.g. turned Right instead of Left),
            // still execute the world turn down that branch while incurring the Recalculating penalty.
            if (roadChunk != null && roadChunk.IsFork && (recordedManeuver == DashboardNavigationSource.Maneuver.Left || recordedManeuver == DashboardNavigationSource.Maneuver.Right))
            {
                if (RouteController.Instance != null)
                {
                    RouteController.Instance.ExecuteWorldTurn(roadChunk, recordedManeuver);
                }
            }
        }
    }

    /// <summary>
    /// Executes a violent, fatal crash when the player fails to steer left or right at a Y-fork
    /// and plows straight into the central divider barrier / crash attenuator.
    /// </summary>
    private void TriggerForkCentralCrash()
    {
        Debug.LogWarning($"<color=red>[TurnTrigger] FATAL CRASH!</color> Player drove straight into the Y-Fork central divider at Z={transform.position.z + roadChunk.IntersectionCenterZ:F1}!");

        // 1. Report missed turn to RouteController
        if (RouteController.Instance != null)
        {
            DashboardNavigationSource.Maneuver expectedManeuver = GetExpectedManeuver();
            RouteController.Instance.ReportTurnOutcome(this, DashboardNavigationSource.Maneuver.Straight, expectedManeuver, false);
        }

        // 2. Heavy impact camera shake
        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(1.2f, 0.9f);
        }

        // 3. Immediately halt world forward movement
        if (WorldSpeedController.Instance != null)
        {
            WorldSpeedController.Instance.ApplySpeedPenalty(999f);
        }

        // 4. Deal fatal damage to VehicleHealth to destroy vehicle, trigger explosion VFX/SFX, and fail delivery
        VehicleHealth health = (VehicleMovement.Instance != null)
            ? VehicleMovement.Instance.GetComponent<VehicleHealth>()
            : UnityEngine.Object.FindFirstObjectByType<VehicleHealth>();

        if (health != null)
        {
            health.TakeDamage(health.MaxHealth);
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.FailDelivery();
        }
    }

    private DashboardNavigationSource.Maneuver GetExpectedManeuver()
    {
        if (roadChunk != null && roadChunk.TargetManeuver != DashboardNavigationSource.Maneuver.Straight)
        {
            return roadChunk.TargetManeuver;
        }

        if (RouteController.Instance != null && RouteController.Instance.HasActiveManeuver)
        {
            return RouteController.Instance.CurrentManeuverDirection;
        }

        // Fallback directly to chunk type
        return roadChunk.ChunkType switch
        {
            RoadChunkType.TurnLeft => DashboardNavigationSource.Maneuver.Left,
            RoadChunkType.TurnRight => DashboardNavigationSource.Maneuver.Right,
            RoadChunkType.YFork => DashboardNavigationSource.Maneuver.Left,
            _ => DashboardNavigationSource.Maneuver.Straight
        };
    }
}

