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
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject deliveryCompletedPanel;
    [SerializeField] private GameObject pausePanel;

    [Header("Delivery Timer")]
    [SerializeField] private DoomDriver.UI.DeliveryTimerUI deliveryTimer;

    [Header("Distance & Score Display")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text highscoreText;
    [SerializeField] private TMP_Text finalDistanceText;

    private int lastDisplayedMeters = -1;

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

        if (DeliveryProgressTracker.Instance != null && highscoreText != null)
        {
            highscoreText.text = $"Highscore: {Mathf.FloorToInt(DeliveryProgressTracker.Instance.Highscore)}";
        }

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
            case GameState.OrderSelection:
            case GameState.DeliveryActive:
                if (gameOverPanel != null) gameOverPanel.SetActive(false);
                if (deliveryCompletedPanel != null) deliveryCompletedPanel.SetActive(false);
                break;

            case GameState.DeliveryCompleted:
                if (gameOverPanel != null) gameOverPanel.SetActive(false);
                if (deliveryCompletedPanel != null)
                {
                    deliveryCompletedPanel.SetActive(true);
                }
                break;

            case GameState.DeliveryFailed:
                if (deliveryCompletedPanel != null) deliveryCompletedPanel.SetActive(false);
                if (gameOverPanel != null)
                {
                    gameOverPanel.SetActive(true);
                }

                if (finalDistanceText != null)
                {
                    int finalMeters = DeliveryProgressTracker.Instance != null 
                        ? Mathf.FloorToInt(DeliveryProgressTracker.Instance.CurrentDistance) 
                        : (lastDisplayedMeters > 0 ? lastDisplayedMeters : 0);

                    finalDistanceText.text = $"You managed to scroll for {finalMeters} meters";
                }
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

    private void HandleDistanceUpdated(float distance)
    {
        int meters = Mathf.FloorToInt(distance);
        if (meters == lastDisplayedMeters) return;
        lastDisplayedMeters = meters;

        if (scoreText != null)
        {
            if (DeliveryProgressTracker.Instance != null && DeliveryProgressTracker.Instance.TargetDistance > 0f)
            {
                int target = Mathf.FloorToInt(DeliveryProgressTracker.Instance.TargetDistance);
                scoreText.text = $"{meters} / {target} m";
            }
            else
            {
                scoreText.text = $"{meters} m";
            }
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
            else if (gameOverPanel == null && string.Equals(t.name, "GameOverPanel", System.StringComparison.OrdinalIgnoreCase))
            {
                gameOverPanel = t.gameObject;
            }
            else if (scoreText == null && string.Equals(t.name, "Score", System.StringComparison.OrdinalIgnoreCase))
            {
                scoreText = t.GetComponent<TMP_Text>();
            }
            else if (highscoreText == null && string.Equals(t.name, "HighScore", System.StringComparison.OrdinalIgnoreCase))
            {
                highscoreText = t.GetComponent<TMP_Text>();
            }
            else if (finalDistanceText == null && string.Equals(t.name, "FinalDistanceText", System.StringComparison.OrdinalIgnoreCase))
            {
                finalDistanceText = t.GetComponent<TMP_Text>();
            }
            else if (deliveryCompletedPanel == null && (t.GetComponent<DeliverySummaryUI>() != null || string.Equals(t.name, "DeliverySummaryPanel", System.StringComparison.OrdinalIgnoreCase) || string.Equals(t.name, "DeliverySummaryCanvas", System.StringComparison.OrdinalIgnoreCase)))
            {
                deliveryCompletedPanel = t.gameObject;
            }
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
