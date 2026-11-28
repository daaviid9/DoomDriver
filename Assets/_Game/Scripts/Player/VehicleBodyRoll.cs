using UnityEngine;

namespace DoomDriver.Player
{
    /// <summary>
    /// Applies visual body roll (tilt) to the vehicle transform based on lateral velocity and speed.
    /// </summary>
    public class VehicleBodyRoll : MonoBehaviour
    {
        [Header("Body Tilt (Roll) Settings")]
        [SerializeField] private float maxTiltAngle = 2f;
        [SerializeField] private float tiltSmoothing = 10f;

        private float currentTilt = 0f;

        public void UpdateAnalogRoll(float lateralVelocity, float maxSteerSpeed, float speedFactor)
        {
            float speedRatio = maxSteerSpeed > 0.01f ? (lateralVelocity / maxSteerSpeed) : 0f;
            float targetTilt = -speedRatio * maxTiltAngle * speedFactor;
            targetTilt = Mathf.Clamp(targetTilt, -maxTiltAngle * 1.5f, maxTiltAngle * 1.5f);

            ApplyTilt(targetTilt);
        }

        public void UpdateDiscreteRoll(float movementDelta)
        {
            float targetTilt = 0f;
            if (Time.deltaTime > 0.0001f)
            {
                targetTilt = -(movementDelta / Time.deltaTime) * (maxTiltAngle / 2f);
            }

            targetTilt = Mathf.Clamp(targetTilt, -maxTiltAngle, maxTiltAngle);
            ApplyTilt(targetTilt);
        }

        public void RecoverToNeutral()
        {
            ApplyTilt(0f);
        }

        public void ResetRoll()
        {
            currentTilt = 0f;
            transform.localRotation = Quaternion.identity;
        }

        private void ApplyTilt(float targetTilt)
        {
            currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSmoothing);
            transform.localRotation = Quaternion.Euler(0f, 0f, currentTilt);
        }
    }
}

