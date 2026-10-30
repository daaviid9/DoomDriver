using UnityEngine;

/// <summary>
/// Detects collisions with road obstacles, applies damage to VehicleHealth,
/// requests speed penalties from WorldSpeedController, and fires collision events.
/// Strictly decoupled from VFX particles, animations, and audio.
/// </summary>
public class VehicleCollisionHandler : MonoBehaviour
{
    [Header("Default Damage by Category")]
    [SerializeField] private int carDamage = 100;
    [SerializeField] private int barricadeDamage = 50;
    [SerializeField] private int edgeDamage = 30;

    private VehicleHealth health;

    /// <summary>
    /// Event fired when a minor obstacle (e.g. cone/curb) is hit.
    /// VehicleVFX listens to this to trigger the hop/bump animation.
    /// </summary>
    public event System.Action OnObstacleBump;

    /// <summary>
    /// Event fired on any obstacle collision. Passes (obstacleType, damage).
    /// </summary>
    public event System.Action<ObstacleType, int> OnObstacleHit;

    private void Awake()
    {
        if (health == null) health = GetComponent<VehicleHealth>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Obstacle")) return;

        ObstacleData data = other.GetComponent<ObstacleData>();
        int damage = edgeDamage;
        ObstacleType type = ObstacleType.Edge;

        if (data != null)
        {
            type = data.obstacleType;
            switch (type)
            {
                case ObstacleType.Car:
                    damage = carDamage;
                    break;
                case ObstacleType.Barricade:
                    damage = barricadeDamage;
                    break;
                case ObstacleType.Edge:
                    damage = edgeDamage;
                    break;
                default:
                    damage = data.damageAmount;
                    break;
            }
        }

        // Apply speed penalties based on collision type
        if (damage >= 50)
        {
            if (WorldSpeedController.Instance != null)
            {
                WorldSpeedController.Instance.ApplySpeedPenalty(7f);
            }
            else
            {
                WorldMover.moveSpeed = Mathf.Max(5f, WorldMover.moveSpeed - 7f);
            }
        }
        else
        {
            OnObstacleBump?.Invoke();

            if (WorldSpeedController.Instance != null)
            {
                WorldSpeedController.Instance.ApplySpeedPenalty(4f);
            }
            else
            {
                WorldMover.moveSpeed = Mathf.Max(5f, WorldMover.moveSpeed - 4f);
            }
        }

        // Camera Shake
        if (CameraShake.Instance != null && damage > 0)
        {
            float shakePower = damage >= 50 ? 0.6f : 0.25f;
            CameraShake.Instance.Shake(0.3f, shakePower);
        }

        // Fire collision event
        OnObstacleHit?.Invoke(type, damage);

        // Apply damage to health
        if (health != null)
        {
            health.TakeDamage(damage);
        }

        // Destroy the obstacle GameObject
        Destroy(other.gameObject);
    }
}
