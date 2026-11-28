using UnityEngine;

namespace DoomDriver.Player
{
    /// <summary>
    /// Controls visual rotation of the vehicle steering wheel based on steering input or scripted turn maneuvers.
    /// </summary>
    public class SteeringWheelVisual : MonoBehaviour
    {
        [Header("Steering Wheel Settings")]
        [SerializeField] private Transform steeringWheel;
        [SerializeField] private float maxSteeringAngle = 90f;
        [SerializeField] private float steeringRotationSpeed = 10f;

        public float MaxSteeringAngle => maxSteeringAngle;

        public void UpdateNormalizedTarget(float normalizedInput)
        {
            if (steeringWheel == null) return;

            float targetAngle = Mathf.Clamp(normalizedInput, -1f, 1f) * maxSteeringAngle;
            Quaternion targetRot = Quaternion.Euler(0f, 0f, targetAngle);
            steeringWheel.localRotation = Quaternion.Lerp(steeringWheel.localRotation, targetRot, Time.deltaTime * steeringRotationSpeed);
        }

        public void SetDirectAngle(float angle)
        {
            if (steeringWheel == null) return;
            steeringWheel.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        public void ResetRotation()
        {
            if (steeringWheel == null) return;
            steeringWheel.localRotation = Quaternion.identity;
        }
    }
}

