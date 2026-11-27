using UnityEngine;
using UnityEngine.UI;

namespace DoomDriver.Phone
{
    /// <summary>
    /// Coordinates phone input, hand raising, reels playback, and delegates FOMO,
    /// camera focus, and post-processing to dedicated single-responsibility components.
    /// </summary>
    public class PhoneSystem : MonoBehaviour
    {
        public enum PhoneMode { OrderSelection, DeliveryActive }

        [Header("UI & Hand References")]
        [SerializeField] private ReelsManager reelsManager;
        [SerializeField] private HandController handController;

        [Header("Modular Sub-Components")]
        [SerializeField] private FomoMeter fomoMeter;
        [SerializeField] private PhoneCameraFocusController cameraFocus;
        [SerializeField] private FomoPostProcessController postProcessController;

        public float FomoValue => fomoMeter != null ? fomoMeter.Value : 100f;

        private InputSystem_Actions controls;
        private bool isScrolling = false;
        private PhoneMode currentMode = PhoneMode.OrderSelection;

        private void Awake()
        {
            PhoneSystem[] allSystems = FindObjectsByType<PhoneSystem>(FindObjectsSortMode.None);
            if (allSystems.Length > 1 && gameObject.name != "Main Camera")
            {
                Debug.LogWarning($"<color=cyan>[PhoneSystem]</color> Disabling duplicate on '{gameObject.name}'.");
                enabled = false;
                return;
            }

            controls = new InputSystem_Actions();
            controls.Player.Jump.performed += _ => SetScrolling(true);
            controls.Player.Jump.canceled += _ => SetScrolling(false);

            ResolveSubComponents();
        }

        private void ResolveSubComponents()
        {
            if (fomoMeter == null) fomoMeter = GetComponent<FomoMeter>();
            if (cameraFocus == null) cameraFocus = GetComponent<PhoneCameraFocusController>();
            if (postProcessController == null) postProcessController = GetComponent<FomoPostProcessController>();

            if (fomoMeter == null) Debug.LogWarning("[PhoneSystem] Missing FomoMeter component on " + gameObject.name);
            if (cameraFocus == null) Debug.LogWarning("[PhoneSystem] Missing PhoneCameraFocusController component on " + gameObject.name);
            if (postProcessController == null) Debug.LogWarning("[PhoneSystem] Missing FomoPostProcessController component on " + gameObject.name);
        }

        private void OnEnable()
        {
            controls?.Enable();
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            controls?.Disable();
            UnsubscribeFromEvents();
        }

        private void Start()
        {
            SubscribeToEvents();

            if (GameManager.Instance != null &&
               (GameManager.Instance.CurrentState == GameState.DayBriefing ||
                GameManager.Instance.CurrentState == GameState.OrderSelection ||
                GameManager.Instance.CurrentState == GameState.DaySummary))
            {
                cameraFocus?.ApplyLobbyCameraInstant();
            }

            ApplyCurrentModeState();
        }

        private void SubscribeToEvents()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnDayBriefingStarted -= OnOrderSelectionStarted;
                GameManager.Instance.OnDayBriefingStarted += OnOrderSelectionStarted;

                GameManager.Instance.OnOrderSelectionStarted -= OnOrderSelectionStarted;
                GameManager.Instance.OnOrderSelectionStarted += OnOrderSelectionStarted;

                GameManager.Instance.OnDaySummaryStarted -= OnOrderSelectionStarted;
                GameManager.Instance.OnDaySummaryStarted += OnOrderSelectionStarted;

                GameManager.Instance.OnDeliveryStarted -= OnDeliveryStarted;
                GameManager.Instance.OnDeliveryStarted += OnDeliveryStarted;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnDayBriefingStarted -= OnOrderSelectionStarted;
                GameManager.Instance.OnOrderSelectionStarted -= OnOrderSelectionStarted;
                GameManager.Instance.OnDaySummaryStarted -= OnOrderSelectionStarted;
                GameManager.Instance.OnDeliveryStarted -= OnDeliveryStarted;
            }
        }


        private void OnOrderSelectionStarted() => SwitchMode(PhoneMode.OrderSelection);
        private void OnDeliveryStarted() => SwitchMode(PhoneMode.DeliveryActive);

        private void SwitchMode(PhoneMode mode)
        {
            currentMode = mode;
            Debug.Log($"<color=cyan>[PhoneSystem]</color> Switched PhoneMode to: {mode}");
            ApplyCurrentModeState();
        }

        private void SetScrolling(bool scrolling)
        {
            if (isScrolling == scrolling) return;
            isScrolling = scrolling;
            ApplyCurrentModeState();
        }

        private void ApplyCurrentModeState()
        {
            bool isLobby = currentMode == PhoneMode.OrderSelection;
            bool isDriving = currentMode == PhoneMode.DeliveryActive;
            bool showPhoneInHand = isLobby || isScrolling;

            fomoMeter?.SetState(isDriving, isScrolling);
            cameraFocus?.SetFocusState(isLobby, isScrolling);
            postProcessController?.SetLobbyMode(isLobby);

            reelsManager?.SetActive(isDriving && isScrolling);
            handController?.SetRaised(showPhoneInHand);
        }
    }
}
