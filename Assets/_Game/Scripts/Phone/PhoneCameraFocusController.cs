using UnityEngine;

namespace DoomDriver.Phone
{
    /// <summary>
    /// Controls camera FOV and pitch tilt transitions for lobby order selection vs. driving/phone focus.
    /// Adheres to SoC: strictly manages camera transform and fieldOfView.
    /// </summary>
    public class PhoneCameraFocusController : MonoBehaviour
    {
        [Header("Camera Zoom & FOV Settings")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private float normalFOV = 60f;
        [SerializeField] private float phoneFOV = 60f;
        [SerializeField, Range(25f, 65f)] private float orderSelectionFOV = 45.5f;
        [SerializeField, Range(0f, 25f)] private float orderSelectionPitchTilt = 14.1f;
        [SerializeField] private float fovTransitionSpeed = 3f;

        private Quaternion defaultCameraRotation;
        private bool hasCachedCameraRotation = false;
        private bool isLobbyMode = true;
        private bool isPhoneRaised = false;

        private void Awake()
        {
            CacheCamera();
        }

        public void SetFocusState(bool lobbyMode, bool phoneRaised)
        {
            isLobbyMode = lobbyMode;
            isPhoneRaised = phoneRaised;
        }

        public void ApplyLobbyCameraInstant()
        {
            CacheCamera();
            if (mainCamera == null) return;

            mainCamera.fieldOfView = orderSelectionFOV;
            mainCamera.transform.localRotation = defaultCameraRotation * Quaternion.Euler(orderSelectionPitchTilt, 0f, 0f);
        }

        private void CacheCamera()
        {
            if (mainCamera == null) mainCamera = GetComponent<Camera>() ?? Camera.main;
            if (mainCamera != null && !hasCachedCameraRotation)
            {
                defaultCameraRotation = mainCamera.transform.localRotation;
                hasCachedCameraRotation = true;
            }
        }

        private void Update()
        {
            if (mainCamera == null) return;

            float targetFov = isLobbyMode
                ? orderSelectionFOV
                : (isPhoneRaised ? phoneFOV : normalFOV);

            Quaternion targetRot = isLobbyMode
                ? defaultCameraRotation * Quaternion.Euler(orderSelectionPitchTilt, 0f, 0f)
                : defaultCameraRotation;

            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFov, Time.deltaTime * fovTransitionSpeed);
            mainCamera.transform.localRotation = Quaternion.Slerp(mainCamera.transform.localRotation, targetRot, Time.deltaTime * fovTransitionSpeed);
        }
    }
}
