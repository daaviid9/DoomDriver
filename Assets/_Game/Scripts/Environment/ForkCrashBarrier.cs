using UnityEngine;

/// <summary>
/// Attached to the central crash barrier ('Road_Barrier_Crash') on Y-fork road chunks.
/// Detects physical collision with the player vehicle and triggers an instant fatal crash.
/// Immune to ObstacleSpawner.ClearObstaclesInZRange because it does not use the 'Obstacle' tag.
/// Adheres to AGENTS.md conventions.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ForkCrashBarrier : MonoBehaviour
{
    private bool hasCrashed = false;

    private void Awake()
    {
        // Ensure collider is configured as a trigger so it detects player penetration smoothly
        if (TryGetComponent<Collider>(out var col))
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasCrashed) return;

        // Verify if colliding object belongs to the player vehicle
        if (other.GetComponentInParent<VehicleHealth>() != null || 
            other.GetComponentInParent<VehicleMovement>() != null ||
            other.CompareTag("Player"))
        {
            TriggerFatalCrash();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasCrashed) return;

        if (collision.collider.GetComponentInParent<VehicleHealth>() != null || 
            collision.collider.GetComponentInParent<VehicleMovement>() != null ||
            collision.collider.CompareTag("Player"))
        {
            TriggerFatalCrash();
        }
    }

    /// <summary>
    /// Executes a violent, lethal crash when the player vehicle physically collides with this barrier.
    /// </summary>
    public void TriggerFatalCrash()
    {
        if (hasCrashed) return;
        hasCrashed = true;

        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.DeliveryActive) return;

        Debug.LogWarning($"<color=red>[ForkCrashBarrier] PHYSICAL CRASH!</color> Player vehicle impacted '{gameObject.name}'!");

        // 1. Notify RouteController of missed turn / crash
        TurnTrigger turnTrigger = GetComponentInParent<TurnTrigger>();
        if (turnTrigger != null && RouteController.Instance != null)
        {
            DashboardNavigationSource.Maneuver expectedManeuver = (turnTrigger.Chunk != null && 
                (turnTrigger.Chunk.TargetManeuver == DashboardNavigationSource.Maneuver.Right || turnTrigger.Chunk.TargetManeuver == DashboardNavigationSource.Maneuver.ForkRight))
                ? DashboardNavigationSource.Maneuver.ForkRight
                : DashboardNavigationSource.Maneuver.ForkLeft;
            RouteController.Instance.ReportTurnOutcome(turnTrigger, DashboardNavigationSource.Maneuver.Straight, expectedManeuver, false);
        }

        // 2. Violent camera shake
        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(1.3f, 1.0f);
        }

        // 3. Immediately halt world forward movement
        if (WorldSpeedController.Instance != null)
        {
            WorldSpeedController.Instance.ApplySpeedPenalty(999f);
        }

        // 4. Deal fatal damage to VehicleHealth to destroy vehicle, trigger explosion VFX/SFX, and fail delivery
        VehicleHealth health = (VehicleMovement.Instance != null)
            ? VehicleMovement.Instance.GetComponent<VehicleHealth>()
            : Object.FindFirstObjectByType<VehicleHealth>();

        if (health != null)
        {
            health.TakeDamage(health.MaxHealth);
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.FailDelivery();
        }
    }
}
