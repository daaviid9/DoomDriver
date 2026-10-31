using UnityEngine;

/// <summary>
/// Moves road chunks and obstacles backwards along the Z-axis to create the illusion of forward vehicle motion.
/// Reads forward movement speed directly from WorldSpeedController (Single Source of Truth).
/// </summary>
public class WorldMover : MonoBehaviour
{
    private void Update()
    {
        float speed = WorldSpeedController.Instance != null ? WorldSpeedController.Instance.CurrentSpeed : 0f;

        // Don't move if speed is 0 or if delivery failed
        if (speed <= 0f) return;
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DeliveryFailed) return;

        // Move the object backwards (towards the player)
        transform.Translate(Vector3.back * speed * Time.deltaTime, Space.World);

        // Cleanup: If the object represents an obstacle and is well behind the player, destroy it
        if (transform.position.z < -15f && gameObject.CompareTag("Obstacle"))
        {
            Destroy(gameObject);
        }
    }
}
