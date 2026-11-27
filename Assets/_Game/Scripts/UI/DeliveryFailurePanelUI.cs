using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DoomDriver.Economy;

namespace DoomDriver.UI
{
    /// <summary>
    /// Base presentation (View) for delivery failure screens (VehicleCrash and TimeExpired).
    /// Adheres to Separation of Concerns (SoC):
    /// - Does NOT calculate penalties or modify wallet funds (see DeliveryPenaltyManager).
    /// - Only populates itemized penalty amounts and handles the return to lobby action.
    /// </summary>
    public abstract class DeliveryFailurePanelUI : MonoBehaviour
    {
        [Header("UI Panels & Groups")]
        [SerializeField] private CanvasGroup panelGroup;

        [Header("Itemized Penalty Amount Displays")]
        [SerializeField] private TMP_Text fee1AmountText;
        [SerializeField] private TMP_Text fee2AmountText;
        [SerializeField] private TMP_Text fee3AmountText;

        [Header("Total Penalty Display")]
        [SerializeField] private TMP_Text totalLossText;

        [Header("Action Button")]
        [SerializeField] private Button returnButton;

        private bool isShowing = false;

        /// <summary>
        /// The specific failure reason this panel responds to.
        /// </summary>
        protected abstract FailureReason HandledReason { get; }

        protected virtual void Awake()
        {
            EnsurePanelGroup();
            SetVisible(false);

            if (returnButton != null)
            {
                returnButton.onClick.RemoveListener(HandleReturnClicked);
                returnButton.onClick.AddListener(HandleReturnClicked);
            }
        }

        protected virtual void OnEnable()
        {
            EnsurePanelGroup();

            if (DeliveryPenaltyManager.Instance != null)
            {
                DeliveryPenaltyManager.Instance.OnPenaltySettled -= HandlePenaltySettled;
                DeliveryPenaltyManager.Instance.OnPenaltySettled += HandlePenaltySettled;
            }

            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DeliveryFailed)
            {
                var result = DeliveryPenaltyManager.Instance != null ? DeliveryPenaltyManager.Instance.LatestResult : null;
                if (result != null && result.Reason == HandledReason)
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

        protected virtual void OnDisable()
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

        protected virtual void Update()
        {
            if (!isShowing) return;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame ||
                                     keyboard.numpadEnterKey.wasPressedThisFrame ||
                                     keyboard.spaceKey.wasPressedThisFrame))
            {
                HandleReturnClicked();
            }
        }

        private void EnsurePanelGroup()
        {
            if (panelGroup == null)
            {
                panelGroup = GetComponent<CanvasGroup>() ?? GetComponentInChildren<CanvasGroup>(true);
            }
            if (panelGroup == null)
            {
                panelGroup = gameObject.AddComponent<CanvasGroup>();
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
            if (result == null || result.Reason != HandledReason) return;
            if (isShowing) return;

            isShowing = true;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

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
                Debug.Log($"<color=cyan>[{GetType().Name}]</color> Returning to Order Selection Lobby...");
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

