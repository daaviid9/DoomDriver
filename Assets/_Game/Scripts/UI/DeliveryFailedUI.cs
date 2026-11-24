using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DoomDriver.Economy;

namespace DoomDriver.UI
{
    /// <summary>
    /// Pure Presentation (View) for the Delivery Failed / Vehicle Crash screen (HP reaches 0).
    /// Adheres to Separation of Concerns (SoC):
    /// - Does NOT calculate penalties or modify wallet funds (see DeliveryPenaltyManager).
    /// - Does NOT check vehicle health or game rules.
    /// - Only populates itemized penalty amounts and handles the return to lobby action.
    /// </summary>
    public class DeliveryFailedUI : MonoBehaviour
    {
        public static DeliveryFailedUI Instance { get; private set; }

        [Header("UI Panels & Groups")]
        [SerializeField] private CanvasGroup panelGroup;

        [Header("Itemized Penalty Amount Displays")]
        [Tooltip("Displays the Tow Truck fee amount (e.g. '- € 7.00')")]
        [SerializeField] private TMP_Text fee1AmountText;

        [Tooltip("Displays the Fleet Repair Deductible amount (e.g. '- € 5.00')")]
        [SerializeField] private TMP_Text fee2AmountText;

        [Tooltip("Displays the Cancelled Order Fee amount (e.g. '- € 3.00')")]
        [SerializeField] private TMP_Text fee3AmountText;

        [Header("Total Penalty Display")]
        [Tooltip("Displays total financial deduction (e.g. '- € 15.00')")]
        [SerializeField] private TMP_Text totalLossText;

        [Header("Action Button")]
        [SerializeField] private Button returnButton;

        private bool isShowing = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            if (panelGroup == null) panelGroup = GetComponent<CanvasGroup>() ?? GetComponentInChildren<CanvasGroup>(true);
            if (panelGroup == null) panelGroup = gameObject.AddComponent<CanvasGroup>();

            SetVisible(false);

            if (returnButton != null)
            {
                returnButton.onClick.RemoveListener(HandleReturnClicked);
                returnButton.onClick.AddListener(HandleReturnClicked);
            }
        }

        private void OnEnable()
        {
            if (panelGroup == null) panelGroup = GetComponent<CanvasGroup>() ?? GetComponentInChildren<CanvasGroup>(true);
            if (panelGroup == null) panelGroup = gameObject.AddComponent<CanvasGroup>();

            if (DeliveryPenaltyManager.Instance != null)
            {
                DeliveryPenaltyManager.Instance.OnPenaltySettled -= HandlePenaltySettled;
                DeliveryPenaltyManager.Instance.OnPenaltySettled += HandlePenaltySettled;
            }

            // If GameUIController activated this panel or state is already DeliveryFailed:
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DeliveryFailed)
            {
                var result = DeliveryPenaltyManager.Instance != null ? DeliveryPenaltyManager.Instance.LatestResult : null;
                if (result != null && result.Reason == FailureReason.VehicleCrash)
                {
                    HandlePenaltySettled(result);
                }
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            }
        }

        private void OnDisable()
        {
            if (DeliveryPenaltyManager.Instance != null)
            {
                DeliveryPenaltyManager.Instance.OnPenaltySettled -= HandlePenaltySettled;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }

            SetVisible(false);
        }

        private void Update()
        {
            if (!isShowing) return;

            // Keyboard shortcut: Enter or Space triggers return to garage/lobby
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame ||
                                     keyboard.numpadEnterKey.wasPressedThisFrame ||
                                     keyboard.spaceKey.wasPressedThisFrame))
            {
                HandleReturnClicked();
            }
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (state != GameState.DeliveryFailed)
            {
                SetVisible(false);
            }
        }

        private void HandlePenaltySettled(DeliveryFailureResult result)
        {
            // Pure crash screen: only handle VehicleCrash
            if (result == null || result.Reason != FailureReason.VehicleCrash) return;

            if (isShowing) return;
            isShowing = true;

            // Unlock and show cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Display itemized amounts
            if (fee1AmountText != null) fee1AmountText.text = $"- € {result.Fee1:F2}";
            if (fee2AmountText != null) fee2AmountText.text = $"- € {result.Fee2:F2}";
            if (fee3AmountText != null) fee3AmountText.text = $"- € {result.Fee3:F2}";
            if (totalLossText != null) totalLossText.text = $"- € {result.TotalPenalty:F2}";

            SetVisible(true);

            if (returnButton != null)
            {
                returnButton.interactable = true;
                returnButton.Select();
            }
        }

        private void HandleReturnClicked()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DeliveryFailed)
            {
                Debug.Log("<color=cyan>[DeliveryFailedUI]</color> Returning to Order Selection Lobby...");
                GameManager.Instance.ReturnToOrderSelection();
            }
        }

        private void SetVisible(bool visible)
        {
            if (panelGroup != null)
            {
                panelGroup.alpha = visible ? 1f : 0f;
                panelGroup.interactable = visible;
                panelGroup.blocksRaycasts = visible;
            }

            if (!visible)
            {
                isShowing = false;
            }
        }
    }
}
