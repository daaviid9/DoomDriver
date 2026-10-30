using UnityEngine;

/// <summary>
/// Moves road chunks and obstacles backwards along the Z-axis to create the illusion of forward vehicle motion.
/// World movement speed is controlled exclusively by WorldSpeedController.
/// </summary>
public class WorldMover : MonoBehaviour
{
    [Tooltip("Global movement speed of the world along the Z axis.")]
    public static float moveSpeed = 0f; // Default 0f (vehicle is parked in lobby)

    void Update()
    {
        // Don't move if speed is 0 or if delivery failed
        if (moveSpeed <= 0f) return;
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DeliveryFailed) return;

        // Move the object backwards (towards the player)
        transform.Translate(Vector3.back * moveSpeed * Time.deltaTime, Space.World);

        // Cleanup: If the object represents an obstacle and is well behind the player, destroy it
        if (transform.position.z < -15f && gameObject.CompareTag("Obstacle"))
        {
            Destroy(gameObject);
        }
    }
}
