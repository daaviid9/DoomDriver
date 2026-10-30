using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Video;

public class PhoneSystem : MonoBehaviour
{
    [Header("UI References")]
    public Slider fomoSlider;       
    public ReelsManager reelsManager;  // Link to your ReelsManager script

    [Header("Hand Reference")]
    public HandController handController;

    [Header("Post-Processing Settings")]
    public Volume postProcessVolume; 
    public float maxVignetteIntensity = 1f;
    public float vignettePower = 2f;
    
    [Header("Values")]
    public float fomoValue = 100f;
    public float drainRate = 8f;   
    public float refillRate = 20f;  
    
    [Header("Camera Zoom & FOV Settings")]
    public Camera mainCamera;
    [Tooltip("Default wide FOV when looking at the road while driving")]
    public float normalFOV = 60f;
    [Tooltip("Moderate FOV when holding Space to scroll reels during active driving")]
    public float phoneFOV = 40f; 
    [Tooltip("FOV during OrderSelection in lobby (calibrated for phone framing)")]
    [Range(25f, 65f)]
    public float orderSelectionFOV = 40.8f;
    [Tooltip("Downward pitch tilt angle (in degrees) in lobby to look down at the phone")]
    [Range(0f, 25f)]
    public float orderSelectionPitchTilt = 17.6f;
    [Tooltip("Speed of camera FOV and effect transitions")]
    public float fovTransitionSpeed = 3f;

    [Header("Lobby / OrderSelection Presentation (Independent from FOMO)")]
    [Tooltip("Enable depth-of-field background blur behind the phone in lobby")]
    public bool enableLobbyBlur = true;
    [Tooltip("Distance from camera where background blur begins (phone is at ~0.6m)")]
    public float lobbyBlurStart = 0.8f;
    [Tooltip("Distance from camera where blur reaches full strength")]
    public float lobbyBlurEnd = 2.2f;
    [Tooltip("Maximum blur radius (Gaussian)")]
    [Range(0.5f, 2f)]
    public float lobbyBlurRadius = 1.5f;

    [Tooltip("Enable subtle background darkening in lobby to focus attention on phone")]
    public bool enableLobbyDarkening = true;
    [Tooltip("Side darkening intensity in lobby")]
    [Range(0f, 1f)]
    public float lobbyVignetteIntensity = 0.45f;
    [Tooltip("Smoothness of the side darkening vignette in lobby")]
    [Range(0.1f, 1f)]
    public float lobbyVignetteSmoothness = 0.6f;

    private InputSystem_Actions controls;
    private bool isScrolling = false;
    private Vignette vignette;
    private ColorAdjustments colorAdjustments;
    private DepthOfField depthOfField;
    private Quaternion defaultCameraRotation;
    private bool hasCachedCameraRotation = false;

    void Awake()
    {
        // Safety: If there is another PhoneSystem component in the scene, keep the one on Main Camera to avoid conflicts
        PhoneSystem[] allSystems = Object.FindObjectsByType<PhoneSystem>(FindObjectsSortMode.None);
        if (allSystems.Length > 1 && gameObject.name != "Main Camera")
        {
            Debug.LogWarning($"[PhoneSystem] Disabling duplicate PhoneSystem on '{gameObject.name}' to prevent camera conflicts.");
            enabled = false;
            return;
        }

        controls = new InputSystem_Actions();
        controls.Player.Jump.performed += ctx => isScrolling = true;
        controls.Player.Jump.canceled += ctx => isScrolling = false;

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null && !hasCachedCameraRotation)
        {
            defaultCameraRotation = mainCamera.transform.localRotation;
            hasCachedCameraRotation = true;
        }

        if (postProcessVolume == null) postProcessVolume = GetComponent<Volume>();
        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            postProcessVolume.profile.TryGet(out vignette);
            postProcessVolume.profile.TryGet(out colorAdjustments);
            postProcessVolume.profile.TryGet(out depthOfField);
        }

        // Safety: If scene has older serialized values, ensure calibrated defaults from playtest are applied
        if (orderSelectionFOV < 36f || orderSelectionFOV > 50f) orderSelectionFOV = 40.8f;
        if (orderSelectionPitchTilt < 10f || orderSelectionPitchTilt > 22f) orderSelectionPitchTilt = 17.6f;
    }

    void OnEnable()
    {
        if (controls != null) controls.Enable();
    }

    void OnDisable()
    {
        if (controls != null) controls.Disable();
    }

    void Start()
    {
        if (fomoSlider != null)
        {
            fomoSlider.maxValue = 100;
            fomoSlider.value = fomoValue;
        }

        // Snap camera immediately to lobby framing at game start so it doesn't slowly zoom from 60
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.OrderSelection && mainCamera != null)
        {
            mainCamera.fieldOfView = orderSelectionFOV;
            Quaternion targetRot = defaultCameraRotation * Quaternion.Euler(orderSelectionPitchTilt, 0f, 0f);
            mainCamera.transform.localRotation = targetRot;
        }
    }

    void Update()
    {
        // Check current game state
        bool isDeliveryActive = GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DeliveryActive;
        bool isOrderSelection = GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.OrderSelection;

        // When NOT in active delivery driving:
        if (!isDeliveryActive)
        {
            // Reset FOMO to full focus (no FOMO meter draining outside active drive)
            fomoValue = 100f;
            if (fomoSlider != null) fomoSlider.value = fomoValue;

            // Reels video is always disabled in lobby / completion
            if (reelsManager != null) reelsManager.SetActive(false);

            // In OrderSelection (Lobby): phone is raised and camera focuses on the phone with optional blur & side darkening
            if (isOrderSelection)
            {
                if (handController != null) handController.SetRaised(true);

                if (mainCamera != null)
                {
                    mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, orderSelectionFOV, Time.deltaTime * fovTransitionSpeed);

                    // Tilt camera downwards slightly to look down at the phone
                    Quaternion targetRot = defaultCameraRotation * Quaternion.Euler(orderSelectionPitchTilt, 0f, 0f);
                    mainCamera.transform.localRotation = Quaternion.Slerp(mainCamera.transform.localRotation, targetRot, Time.deltaTime * fovTransitionSpeed);
                }

                // Background blur (Depth of Field) behind the phone in lobby
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

                // Subtle cinematic side darkening to focus purely on the phone in the center
                if (vignette != null)
                {
                    float targetIntensity = enableLobbyDarkening ? lobbyVignetteIntensity : 0f;
                    float currentInt = vignette.intensity.value;
                    vignette.intensity.Override(Mathf.Lerp(currentInt, targetIntensity, Time.deltaTime * fovTransitionSpeed));
                    vignette.smoothness.Override(lobbyVignetteSmoothness);
                }
                if (colorAdjustments != null)
                {
                    colorAdjustments.postExposure.Override(0f);
                }
            }
            else
            {
                // In DeliveryCompleted or Failed: phone is lowered, camera at normal FOV and normal rotation, disable lobby blur & vignette
                if (handController != null) handController.SetRaised(false);

                if (mainCamera != null)
                {
                    mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, normalFOV, Time.deltaTime * fovTransitionSpeed);
                    mainCamera.transform.localRotation = Quaternion.Slerp(mainCamera.transform.localRotation, defaultCameraRotation, Time.deltaTime * fovTransitionSpeed);
                }

                if (depthOfField != null)
                {
                    depthOfField.mode.Override(DepthOfFieldMode.Off);
                }

                if (vignette != null)
                {
                    float currentInt = vignette.intensity.value;
                    vignette.intensity.Override(Mathf.Lerp(currentInt, 0f, Time.deltaTime * fovTransitionSpeed));
                }
                if (colorAdjustments != null)
                {
                    colorAdjustments.postExposure.Override(0f);
                }
            }

            return;
        }

        // --- ACTIVE DELIVERY LOGIC (Driving) ---

        // Disable lobby depth of field during driving so road visibility is clear
        if (depthOfField != null && depthOfField.mode.value != DepthOfFieldMode.Off)
        {
            depthOfField.mode.Override(DepthOfFieldMode.Off);
        }

        // Ensure camera returns to default level orientation during active driving
        if (mainCamera != null)
        {
            mainCamera.transform.localRotation = Quaternion.Slerp(mainCamera.transform.localRotation, defaultCameraRotation, Time.deltaTime * fovTransitionSpeed);
        }

        // 1. Post-Processing based on FOMO meter
        if (vignette != null)
        {
            float fomoNormalized = fomoValue / 100f;
            float intensityFactor = 1f - Mathf.Pow(fomoNormalized, vignettePower);
            
            vignette.intensity.Override(intensityFactor * maxVignetteIntensity);
            vignette.smoothness.Override(Mathf.Lerp(0.2f, 1f, intensityFactor));

            if (colorAdjustments != null)
            {
                float exposureFactor = Mathf.Pow(intensityFactor, 3f);
                colorAdjustments.postExposure.Override(Mathf.Lerp(0f, -10f, exposureFactor));
            }
        }

        // 2. Scrolling Reels vs Road Watching
        if (isScrolling)
        {
            if (reelsManager != null) reelsManager.SetActive(true);
            if (handController != null) handController.SetRaised(true);
            fomoValue += refillRate * Time.deltaTime;
            
            if (mainCamera != null)
                mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, phoneFOV, Time.deltaTime * fovTransitionSpeed);
        }
        else
        {
            if (reelsManager != null) reelsManager.SetActive(false);
            if (handController != null) handController.SetRaised(false);
            fomoValue -= drainRate * Time.deltaTime;
            
            if (mainCamera != null)
                mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, normalFOV, Time.deltaTime * fovTransitionSpeed);
        }

        fomoValue = Mathf.Clamp(fomoValue, 0f, 100f);
        if (fomoSlider != null) fomoSlider.value = fomoValue;
    }
}
