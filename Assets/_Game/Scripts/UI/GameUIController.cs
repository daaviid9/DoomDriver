using UnityEngine;
using TMPro;

/// <summary>
/// Manages in-game HUD, panels, and score display.
/// Listens to events from GameManager, DeliveryProgressTracker, and PauseController.
/// Strictly decoupled from game state logic and encapsulates all UI elements.
/// </summary>
public class GameUIController : MonoBehaviour
{
    public static GameUIController Instance { get; private set; }

    [Header("UI Panels")]
    [SerializeField] private GameObject deliveryCompletedPanel;
    [SerializeField] private GameObject deliveryFailedPanel;
    [SerializeField] private GameObject deliveryExpiredPanel;
    [SerializeField] private GameObject pausePanel;

    [Header("Delivery Timer")]
    [SerializeField] private DoomDriver.UI.DeliveryTimerUI deliveryTimer;

    [Header("Distance & Score Display")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text highscoreText;

    private int lastDisplayedMeters = -1;
    private int lastDisplayedTargetMeters = -1; 

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        ResolveMissingReferences();
    }

    private void Start()
    {
        SubscribeToEvents();

        // Initialize HUD elements
        HandleDistanceUpdated(0f);
        UpdateShiftHUD();

        // Apply initial panel visibility matching starting game state
        if (GameManager.Instance != null)
        {
            HandleGameStateChanged(GameManager.Instance.CurrentState);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        if (Instance == this) Instance = null;
    }

    private void SubscribeToEvents()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }

        if (PauseController.Instance != null)
        {
            PauseController.Instance.OnPauseToggled += HandlePauseToggled;
        }

        if (DeliveryProgressTracker.Instance != null)
        {
            DeliveryProgressTracker.Instance.OnDistanceUpdated += HandleDistanceUpdated;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }

        if (PauseController.Instance != null)
        {
            PauseController.Instance.OnPauseToggled -= HandlePauseToggled;
        }

        if (DeliveryProgressTracker.Instance != null)
        {
            DeliveryProgressTracker.Instance.OnDistanceUpdated -= HandleDistanceUpdated;
        }
    }

    private void HandleGameStateChanged(GameState state)
    {
        switch (state)
        {
            case GameState.DayBriefing:
            case GameState.DaySummary:
            case GameState.OrderSelection:
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (deliveryFailedPanel != null) deliveryFailedPanel.SetActive(false);
                if (deliveryExpiredPanel != null) deliveryExpiredPanel.SetActive(false);
                if (deliveryCompletedPanel != null) deliveryCompletedPanel.SetActive(false);
                break;

            case GameState.DeliveryActive:
                UpdateShiftHUD();
                if (deliveryCompletedPanel != null) deliveryCompletedPanel.SetActive(false);
                if (deliveryFailedPanel != null) deliveryFailedPanel.SetActive(false);
                if (deliveryExpiredPanel != null) deliveryExpiredPanel.SetActive(false);
                break;

            case GameState.DeliveryCompleted:
                if (deliveryFailedPanel != null) deliveryFailedPanel.SetActive(false);
                if (deliveryExpiredPanel != null) deliveryExpiredPanel.SetActive(false);
                if (deliveryCompletedPanel != null)
                {
                    deliveryCompletedPanel.SetActive(true);
                }
                break;

            case GameState.DeliveryFailed:
                if (deliveryCompletedPanel != null) deliveryCompletedPanel.SetActive(false);
                var reason = GameManager.Instance != null ? GameManager.Instance.LastFailureReason : DoomDriver.Economy.FailureReason.VehicleCrash;

                if (reason == DoomDriver.Economy.FailureReason.VehicleCrash)
                {
                    if (deliveryExpiredPanel != null) deliveryExpiredPanel.SetActive(false);
                    if (deliveryFailedPanel != null) deliveryFailedPanel.SetActive(true);
                }
                else if (reason == DoomDriver.Economy.FailureReason.TimeExpired)
                {
                    if (deliveryFailedPanel != null) deliveryFailedPanel.SetActive(false);
                    if (deliveryExpiredPanel != null) deliveryExpiredPanel.SetActive(true);
                }

                // Restore cursor visibility and release lock so player can click Retry / Exit buttons
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                break;
        }
    }

    private void HandlePauseToggled(bool isPaused)
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(isPaused);
        }
    }

    private void UpdateShiftHUD()
    {
        if (highscoreText != null)
        {
            int day = ShiftManager.Instance != null ? ShiftManager.Instance.CurrentDay : 1;
            int delivered = ShiftManager.Instance != null ? ShiftManager.Instance.DeliveriesCompletedToday : 0;
            int quota = ShiftManager.Instance != null ? ShiftManager.Instance.DailyQuota : 3;
            bool isQuotaMet = ShiftManager.Instance != null && ShiftManager.Instance.IsQuotaMet;

            if (isQuotaMet)
            {
                highscoreText.text = $"Day {day} • Overtime ({delivered}/{quota})";
            }
            else
            {
                highscoreText.text = $"Day {day} • Mission {delivered + 1}/{quota}";
            }
        }
    }

    private void HandleDistanceUpdated(float distance)
    {
        int meters = Mathf.FloorToInt(distance);
        int target = (DeliveryProgressTracker.Instance != null && DeliveryProgressTracker.Instance.TargetDistance > 0f)
            ? Mathf.FloorToInt(DeliveryProgressTracker.Instance.TargetDistance)
            : 0;

        if (meters == lastDisplayedMeters && target == lastDisplayedTargetMeters) return;
        lastDisplayedMeters = meters;
        lastDisplayedTargetMeters = target;

        if (scoreText != null)
        {
            scoreText.text = target > 0 ? $"{meters} / {target} m" : $"{meters} m";
        }
    }

    /// <summary>
    /// Searches scene hierarchy (including inactive panel GameObjects) to automatically bind UI elements.
    /// </summary>
    private void ResolveMissingReferences()
    {
        // Dashboard/phone world-space canvases are not the screen HUD.
        Canvas canvas = null;
        foreach (var candidate in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.InstanceID))
        {
            if (candidate.renderMode == RenderMode.WorldSpace || candidate.GetComponent<DeliverySummaryUI>() != null) continue;
            canvas = candidate;
            break;
        }
        if (canvas == null) return;

        Transform[] allTransforms = canvas.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in allTransforms)
        {
            if (pausePanel == null && string.Equals(t.name, "PausePanel", System.StringComparison.OrdinalIgnoreCase))
            {
                pausePanel = t.gameObject;
            }
            else if (scoreText == null && string.Equals(t.name, "Score", System.StringComparison.OrdinalIgnoreCase))
            {
                scoreText = t.GetComponent<TMP_Text>();
            }
            else if (highscoreText == null && string.Equals(t.name, "HighScore", System.StringComparison.OrdinalIgnoreCase))
            {
                highscoreText = t.GetComponent<TMP_Text>();
            }
        }

        if (deliveryCompletedPanel == null)
        {
            var summaryUI = canvas.GetComponentInChildren<DeliverySummaryUI>(true);
            if (summaryUI != null) deliveryCompletedPanel = summaryUI.gameObject;
        }

        if (deliveryFailedPanel == null)
        {
            var failedUI = canvas.GetComponentInChildren<DoomDriver.UI.DeliveryFailedUI>(true);
            if (failedUI != null) deliveryFailedPanel = failedUI.gameObject;
        }

        if (deliveryExpiredPanel == null)
        {
            var expiredUI = canvas.GetComponentInChildren<DoomDriver.UI.DeliveryExpiredUI>(true);
            if (expiredUI != null) deliveryExpiredPanel = expiredUI.gameObject;
        }

        if (deliveryTimer == null)
        {
            deliveryTimer = canvas.GetComponentInChildren<DoomDriver.UI.DeliveryTimerUI>(true);
            if (deliveryTimer == null)
            {
                Debug.LogWarning("[GameUIController] DeliveryTimerUI not found under Canvas. Please drag the 'DeliveryTimer' prefab under Canvas in your scene.");
            }
        }
    }
}
