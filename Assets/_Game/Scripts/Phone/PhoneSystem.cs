using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Video;

namespace DoomDriver.Phone
{
    /// <summary>
    /// Manages phone UI, FOMO meter, and camera/post-process behaviour for the two gameplay phases.
    /// </summary>
    public class PhoneSystem : MonoBehaviour
    {
        public enum PhoneMode { OrderSelection, DeliveryActive }

        // ---------------------------------------------------------------------
        // Serialized fields – keep private to honour encapsulation
        // ---------------------------------------------------------------------
        [Header("UI References")]
        [SerializeField] private Slider fomoSlider;
        [SerializeField] private ReelsManager reelsManager;

        [Header("Hand Reference")]
        [SerializeField] private HandController handController;

        [Header("Post-Processing Settings")]
        [SerializeField] private Volume postProcessVolume;
        [SerializeField] private float maxVignetteIntensity = 1f;
        [SerializeField] private float vignettePower = 2f;

        [Header("Gameplay Values")]
        [SerializeField] private float fomoValue = 100f;
        [SerializeField] private float drainRate = 8f;
        [SerializeField] private float refillRate = 20f;

        [Header("Camera Zoom & FOV Settings")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private float normalFOV = 60f;
        [SerializeField] private float phoneFOV = 40f;
        [SerializeField, Range(25f, 65f)] private float orderSelectionFOV = 40.8f;
        [SerializeField, Range(0f, 25f)] private float orderSelectionPitchTilt = 17.6f;
        [SerializeField] private float fovTransitionSpeed = 3f;

        [Header("Lobby Presentation (Independent from FOMO)")]
        [SerializeField] private bool enableLobbyBlur = true;
        [SerializeField] private float lobbyBlurStart = 0.8f;
        [SerializeField] private float lobbyBlurEnd = 2.2f;
        [SerializeField, Range(0.5f, 2f)] private float lobbyBlurRadius = 1.5f;
        [SerializeField] private bool enableLobbyDarkening = true;
        [SerializeField, Range(0f, 1f)] private float lobbyVignetteIntensity = 0.45f;
        [SerializeField, Range(0.1f, 1f)] private float lobbyVignetteSmoothness = 0.6f;

        // ---------------------------------------------------------------------
        // Cached component references
        // ---------------------------------------------------------------------
        private InputSystem_Actions controls;
        private bool isScrolling = false;
        private Vignette vignette;
        private ColorAdjustments colorAdjustments;
        private DepthOfField depthOfField;
        private Quaternion defaultCameraRotation;
        private bool hasCachedCameraRotation = false;

        private PhoneMode currentMode = PhoneMode.OrderSelection;

        // =====================================================================
        // Unity lifecycle
        // =====================================================================
        private void Awake()
        {
            // Ensure only one instance on the main camera.
            PhoneSystem[] allSystems = Object.FindObjectsByType<PhoneSystem>(FindObjectsSortMode.None);
            if (allSystems.Length > 1 && gameObject.name != "Main Camera")
            {
                Debug.LogWarning($"<color=cyan>[PhoneSystem]</color> Disabling duplicate on '{gameObject.name}'.");
                enabled = false;
                return;
            }

            controls = new InputSystem_Actions();
            controls.Player.Jump.performed += _ => isScrolling = true;
            controls.Player.Jump.canceled += _ => isScrolling = false;

            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null && !hasCachedCameraRotation)
            {
                defaultCameraRotation = mainCamera.transform.localRotation;
                hasCachedCameraRotation = true;
            }

            // Grab post-process effects if they exist.
            if (postProcessVolume == null) postProcessVolume = GetComponent<Volume>();
            if (postProcessVolume != null && postProcessVolume.profile != null)
            {
                postProcessVolume.profile.TryGet(out vignette);
                postProcessVolume.profile.TryGet(out colorAdjustments);
                postProcessVolume.profile.TryGet(out depthOfField);
            }

            // Clamp legacy serialized values.
            if (orderSelectionFOV < 36f || orderSelectionFOV > 50f) orderSelectionFOV = 40.8f;
            if (orderSelectionPitchTilt < 10f || orderSelectionPitchTilt > 22f) orderSelectionPitchTilt = 17.6f;

            Debug.Log($"<color=cyan>[PhoneSystem]</color> Awake on '{gameObject.name}'. Target lobby FOV: {orderSelectionFOV}");
        }

        private void OnEnable()
        {
            controls?.Enable();
            SubscribeToGameManager();
        }

        private void OnDisable()
        {
            controls?.Disable();
            UnsubscribeFromGameManager();
        }

        private void Start()
        {
            SubscribeToGameManager();

            if (fomoSlider != null)
            {
                fomoSlider.maxValue = 100;
                fomoSlider.value = fomoValue;
            }

            // Initialise camera if we start directly in the lobby.
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.OrderSelection && mainCamera != null)
            {
                ApplyLobbyCameraInstant();
            }
        }

        private void SubscribeToGameManager()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnOrderSelectionStarted -= OnOrderSelectionStarted;
                GameManager.Instance.OnOrderSelectionStarted += OnOrderSelectionStarted;

                GameManager.Instance.OnDeliveryStarted -= OnDeliveryStarted;
                GameManager.Instance.OnDeliveryStarted += OnDeliveryStarted;
            }
        }

        private void UnsubscribeFromGameManager()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnOrderSelectionStarted -= OnOrderSelectionStarted;
                GameManager.Instance.OnDeliveryStarted -= OnDeliveryStarted;
            }
        }

        private void OnOrderSelectionStarted()
        {
            SwitchMode(PhoneMode.OrderSelection);
        }

        private void OnDeliveryStarted()
        {
            SwitchMode(PhoneMode.DeliveryActive);
        }

        private void Update()
        {
            if (currentMode == PhoneMode.OrderSelection)
                HandleLobby();
            else
                HandleDeliveryActive();
        }

        // =====================================================================
        // Mode handling
        // =====================================================================
        private void SwitchMode(PhoneMode mode)
        {
            currentMode = mode;
            Debug.Log($"<color=cyan>[PhoneSystem]</color> Switched PhoneMode to: {mode}");
        }

        // ---------------------------------------------------------------------
        // Lobby (OrderSelection) handling
        // ---------------------------------------------------------------------
        private void HandleLobby()
        {
            // FOMO is reset when not driving.
            fomoValue = 100f;
            if (fomoSlider != null) fomoSlider.value = fomoValue;

            // Ensure reels are off.
            reelsManager?.SetActive(false);

            // Raise phone hand and configure camera.
            handController?.SetRaised(true);
            UpdateLobbyCamera();
            ApplyLobbyPostProcessing();
        }

        private void UpdateLobbyCamera()
        {
            if (mainCamera == null) return;
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, orderSelectionFOV, Time.deltaTime * fovTransitionSpeed);
            Quaternion targetRot = defaultCameraRotation * Quaternion.Euler(orderSelectionPitchTilt, 0f, 0f);
            mainCamera.transform.localRotation = Quaternion.Slerp(mainCamera.transform.localRotation, targetRot, Time.deltaTime * fovTransitionSpeed);
        }

        private void ApplyLobbyCameraInstant()
        {
            if (mainCamera == null) return;
            mainCamera.fieldOfView = orderSelectionFOV;
            Quaternion targetRot = defaultCameraRotation * Quaternion.Euler(orderSelectionPitchTilt, 0f, 0f);
            mainCamera.transform.localRotation = targetRot;
        }

        private void ApplyLobbyPostProcessing()
        {
            // Depth of field blur.
            if (depthOfField != null)
            {
                if (enableLobbyBlur)
                {
                    depthOfField.mode.Override(DepthOfFieldMode.Gaussian);
                    depthOfField.gaussianStart.Override(lobbyBlurStart);
                    depthOfField.gaussianEnd.Override(lobbyBlurEnd);
                    depthOfField.gaussianMaxRadius.Override(lobbyBlurRadius);
                }
                else
                {
                    depthOfField.mode.Override(DepthOfFieldMode.Off);
                }
            }

            // Vignette darkening.
            if (vignette != null)
            {
                float target = enableLobbyDarkening ? lobbyVignetteIntensity : 0f;
                vignette.intensity.Override(Mathf.Lerp(vignette.intensity.value, target, Time.deltaTime * fovTransitionSpeed));
                vignette.smoothness.Override(lobbyVignetteSmoothness);
            }

            // Reset exposure.
            if (colorAdjustments != null) colorAdjustments.postExposure.Override(0f);
        }

        // ---------------------------------------------------------------------
        // Delivery active handling (driving)
        // ---------------------------------------------------------------------
        private void HandleDeliveryActive()
        {
            // Ensure lobby effects are disabled.
            if (depthOfField != null && depthOfField.mode.value != DepthOfFieldMode.Off)
                depthOfField.mode.Override(DepthOfFieldMode.Off);

            // Keep camera upright.
            if (mainCamera != null)
                mainCamera.transform.localRotation = Quaternion.Slerp(mainCamera.transform.localRotation, defaultCameraRotation, Time.deltaTime * fovTransitionSpeed);

            // Update FOMO meter.
            UpdateFomo();

            // Apply post-process based on current FOMO.
            UpdateDrivingPostProcessing();

            // Handle scrolling/reels.
            UpdateScrolling();
        }

        private void UpdateFomo()
        {
            if (isScrolling)
                fomoValue += refillRate * Time.deltaTime;
            else
                fomoValue -= drainRate * Time.deltaTime;

            fomoValue = Mathf.Clamp(fomoValue, 0f, 100f);
            if (fomoSlider != null) fomoSlider.value = fomoValue;
        }

        private void UpdateDrivingPostProcessing()
        {
            if (vignette == null) return;
            float normalized = fomoValue / 100f;
            float intensityFactor = 1f - Mathf.Pow(normalized, vignettePower);
            vignette.intensity.Override(intensityFactor * maxVignetteIntensity);
            vignette.smoothness.Override(Mathf.Lerp(0.2f, 1f, intensityFactor));
            if (colorAdjustments != null)
                colorAdjustments.postExposure.Override(Mathf.Lerp(0f, -10f, Mathf.Pow(intensityFactor, 3f)));
        }

        private void UpdateScrolling()
        {
            if (isScrolling)
            {
                reelsManager?.SetActive(true);
                handController?.SetRaised(true);
                if (mainCamera != null)
                    mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, phoneFOV, Time.deltaTime * fovTransitionSpeed);
            }
            else
            {
                reelsManager?.SetActive(false);
                handController?.SetRaised(false);
                if (mainCamera != null)
                    mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, normalFOV, Time.deltaTime * fovTransitionSpeed);
            }
        }
    }
}
