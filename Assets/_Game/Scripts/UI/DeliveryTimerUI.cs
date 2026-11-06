using System.Globalization;
using UnityEngine;
using TMPro;

namespace DoomDriver.UI
{
    /// <summary>
    /// Presentation component for the active delivery mission timer and overtime penalty display.
    /// Strictly decoupled from gameplay logic; subscribes to DeliveryProgressTracker and GameManager events.
    /// Follows Single Responsibility Principle (SRP).
    /// </summary>
    public class DeliveryTimerUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [Tooltip("TextMeshPro component displaying the remaining delivery time or overtime.")]
        [SerializeField] private TMP_Text timerText;

        [Tooltip("Optional secondary TextMeshPro component displaying the overtime financial penalty.")]
        [SerializeField] private TMP_Text penaltyText;

        [Tooltip("Container GameObject that is toggled active during DeliveryActive and hidden otherwise.")]
        [SerializeField] private GameObject container;

        [Header("Colors & Styling")]
        [Tooltip("Default timer text color when plenty of time remains.")]
        [SerializeField] private Color normalColor = Color.white;

        [Tooltip("Warning timer text color when time is below warning threshold.")]
        [SerializeField] private Color warningColor = new Color(1f, 0.75f, 0.1f);

        [Tooltip("Overtime timer text color when deadline is exceeded.")]
        [SerializeField] private Color overtimeColor = new Color(1f, 0.25f, 0.25f);

        [Tooltip("Remaining seconds threshold to switch to warning color.")]
        [SerializeField] private float warningThresholdSeconds = 10f;

        [Header("Overtime Animation")]
        [Tooltip("Whether the timer pulses/flashes while in overtime.")]
        [SerializeField] private bool flashInOvertime = true;

        [Tooltip("Flashing frequency in Hz.")]
        [SerializeField] private float flashSpeed = 4f;

        private int lastDisplayedSecond = -1;
        private bool isCurrentlyOvertime = false;
        private float cachedPenalty = 0f;

        private void Awake()
        {
            ResolveMissingReferences();
            SetVisible(false);
        }

        private void Start()
        {
            SubscribeToEvents();

            if (GameManager.Instance != null)
            {
                HandleGameStateChanged(GameManager.Instance.CurrentState);
            }
        }

        private void OnEnable()
        {
            SubscribeToEvents();

            if (GameManager.Instance != null)
            {
                HandleGameStateChanged(GameManager.Instance.CurrentState);
            }
        }

        private void OnDisable()
        {
            // Do NOT unsubscribe from GameManager here! If this component or a parent panel is temporarily
            // hidden in OrderSelection, it still needs to hear OnGameStateChanged to wake up when delivery starts.
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void Update()
        {
            // Pulse color when actively in overtime for urgency
            if (isCurrentlyOvertime && flashInOvertime && timerText != null && timerText.enabled)
            {
                float t = Mathf.PingPong(Time.time * flashSpeed, 1f);
                timerText.color = Color.Lerp(overtimeColor, Color.white, t * 0.5f);
            }
        }

        private void SubscribeToEvents()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            }

            if (DeliveryProgressTracker.Instance != null)
            {
                DeliveryProgressTracker.Instance.OnTimerUpdated -= HandleTimerUpdated;
                DeliveryProgressTracker.Instance.OnTimerUpdated += HandleTimerUpdated;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }

            if (DeliveryProgressTracker.Instance != null)
            {
                DeliveryProgressTracker.Instance.OnTimerUpdated -= HandleTimerUpdated;
            }
        }

        private void HandleGameStateChanged(GameState newState)
        {
            bool showTimer = (newState == GameState.DeliveryActive);

            if (!showTimer)
            {
                isCurrentlyOvertime = false;
                lastDisplayedSecond = -1;
                SetVisible(false);
            }
            else
            {
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                }
                SetVisible(true);

                // Refresh immediately when entering active delivery
                if (DeliveryProgressTracker.Instance != null)
                {
                    var tracker = DeliveryProgressTracker.Instance;
                    HandleTimerUpdated(tracker.RemainingTime, tracker.IsOvertime, tracker.OvertimeElapsed, tracker.CurrentOvertimePenalty);
                }
            }
        }

        private void HandleTimerUpdated(float remainingSeconds, bool isOvertime, float overtimeSeconds, float currentPenalty)
        {
            isCurrentlyOvertime = isOvertime;
            cachedPenalty = currentPenalty;

            if (timerText == null) return;

            if (!isOvertime)
            {
                UpdateNormalCountdown(remainingSeconds);
            }
            else
            {
                UpdateOvertimeCountdown(overtimeSeconds, currentPenalty);
            }
        }

        private void UpdateNormalCountdown(float remainingSeconds)
        {
            int secondsInt = Mathf.CeilToInt(remainingSeconds);
            if (secondsInt != lastDisplayedSecond)
            {
                lastDisplayedSecond = secondsInt;
                int minutes = secondsInt / 60;
                int seconds = secondsInt % 60;
                timerText.text = string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", minutes, seconds);
                timerText.color = (remainingSeconds <= warningThresholdSeconds) ? warningColor : normalColor;
            }

            if (penaltyText != null)
            {
                penaltyText.enabled = false;
            }
        }

        private void UpdateOvertimeCountdown(float overtimeSeconds, float currentPenalty)
        {
            int overtimeSecondsInt = Mathf.FloorToInt(overtimeSeconds);
            if (overtimeSecondsInt != lastDisplayedSecond)
            {
                lastDisplayedSecond = overtimeSecondsInt;
                int minutes = overtimeSecondsInt / 60;
                int seconds = overtimeSecondsInt % 60;

                if (penaltyText != null)
                {
                    timerText.text = string.Format(CultureInfo.InvariantCulture, "LATE +{0:00}:{1:00}", minutes, seconds);
                    penaltyText.enabled = true;
                    penaltyText.text = string.Format(CultureInfo.InvariantCulture, "-{0:F2} €", currentPenalty);
                    penaltyText.color = overtimeColor;
                }
                else
                {
                    timerText.text = string.Format(CultureInfo.InvariantCulture, "LATE +{0:00}:{1:00} (-{2:F1}€)", minutes, seconds, currentPenalty);
                }
            }

            if (!flashInOvertime)
            {
                timerText.color = overtimeColor;
            }
        }

        private void SetVisible(bool visible)
        {
            // Always keep this GameObject active so coroutines and event callbacks function.
            // Toggle visibility via TMP_Text enabled state or container if safe.
            if (timerText != null)
            {
                timerText.enabled = visible;
            }

            if (penaltyText != null)
            {
                penaltyText.enabled = visible && isCurrentlyOvertime;
            }

            if (container != null && container != gameObject && !transform.IsChildOf(container.transform))
            {
                container.SetActive(visible);
            }
        }

        /// <summary>
        /// Automatically binds references if left unassigned in the inspector.
        /// </summary>
        private void ResolveMissingReferences()
        {
            if (timerText == null)
            {
                timerText = GetComponent<TMP_Text>();
                if (timerText == null)
                {
                    timerText = GetComponentInChildren<TMP_Text>(true);
                }
            }

            if (container == null && timerText != null && timerText.transform.parent != null 
                && timerText.transform.parent != transform && timerText.transform.parent.GetComponent<Canvas>() == null)
            {
                container = timerText.transform.parent.gameObject;
            }
        }
    }
}
