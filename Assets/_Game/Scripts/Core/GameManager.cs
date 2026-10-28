using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game State Machine")]
    [SerializeField] private GameState currentState = GameState.OrderSelection;
    public GameState CurrentState => currentState;

    /// <summary>
    /// Backward-compatibility property for existing scripts checking if the game has ended in failure.
    /// </summary>
    public bool isGameOver => currentState == GameState.DeliveryFailed;
    public bool isPaused = false;

    // C# Events for decoupled system communication
    public event System.Action<GameState> OnGameStateChanged;
    public event System.Action OnDeliveryStarted;
    public event System.Action OnDeliveryCompleted;
    public event System.Action OnDeliveryFailed;

    [Header("Movement & World Speed")]
    [Tooltip("Starting cruise speed when a delivery begins")]
    public float defaultCruiseSpeed = 12f;
    [Tooltip("How much world speed increases each second during delivery")]
    public float speedIncreaseRate = 0.1f;
    [Tooltip("Maximum allowed world speed (limit)")]
    public float maxSpeed = 30f;

    [Header("Score & Distance System")]
    public float currentScore = 0f;
    private float highscore = 0f;
    [Tooltip("UI Text for current score")]
    public TMP_Text scoreText;
    [Tooltip("UI Text for highscore")]
    public TMP_Text highscoreText;
    [Tooltip("UI Text for final score after failure")]
    public TMP_Text finalDistanceText;

    [Header("UI Panels")]
    public GameObject gameOverPanel;
    public GameObject deliveryCompletedPanel;
    public GameObject pausePanel;

    [Header("Scene Settings")]
    [Tooltip("Name of the scene to return to (e.g., LoadingScreenMenu)")]
    public string mainMenuSceneName = "LoadingScreenMenu";

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Load highscore on start
        highscore = PlayerPrefs.GetFloat("Highscore", 0f);
    }

    private void Start()
    {
        // Start in the parked car lobby by default (car stationary)
        SetState(GameState.OrderSelection);
    }

    private void Update()
    {
        // Pause handling via New Input System
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }

        if (isPaused) return;

        // Active delivery progression
        if (currentState == GameState.DeliveryActive)
        {
            // 1. Distance accumulation
            currentScore += WorldMover.moveSpeed * Time.deltaTime;

            if (scoreText != null)
            {
                scoreText.text = Mathf.FloorToInt(currentScore).ToString() + " m";
            }

            if (finalDistanceText != null)
            {
                finalDistanceText.text = "You managed to scroll for " + Mathf.FloorToInt(currentScore).ToString() + " meters";
            }

            // 2. Gradual acceleration over time
            if (WorldMover.moveSpeed < maxSpeed)
            {
                WorldMover.moveSpeed += speedIncreaseRate * Time.deltaTime;
                if (WorldMover.moveSpeed > maxSpeed)
                {
                    WorldMover.moveSpeed = maxSpeed;
                }
            }
        }

#if UNITY_EDITOR
        // Debug hotkeys to test state machine in the Unity Editor
        HandleDebugInput();
#endif
    }

#if UNITY_EDITOR
    private void HandleDebugInput()
    {
        if (UnityEngine.InputSystem.Keyboard.current == null) return;

        // Enter: Start delivery from lobby
        if (UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame && currentState == GameState.OrderSelection)
        {
            Debug.Log("<color=cyan>[DEBUG]</color> Enter pressed -> Starting Delivery!");
            StartDelivery();
        }
        // C: Complete delivery
        else if (UnityEngine.InputSystem.Keyboard.current.cKey.wasPressedThisFrame && currentState == GameState.DeliveryActive)
        {
            Debug.Log("<color=green>[DEBUG]</color> C pressed -> Completing Delivery!");
            CompleteDelivery();
        }
        // R: Return to Lobby
        else if (UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame && 
                (currentState == GameState.DeliveryCompleted || currentState == GameState.DeliveryFailed))
        {
            Debug.Log("<color=yellow>[DEBUG]</color> R pressed -> Returning to Order Selection Lobby!");
            ReturnToOrderSelection();
        }
    }
#endif

    /// <summary>
    /// Changes the current game state and coordinates world speed, UI panels, and events.
    /// </summary>
    public void SetState(GameState newState)
    {
        currentState = newState;
        Debug.Log($"[GameManager] Game state changed to: {currentState}");

        switch (currentState)
        {
            case GameState.OrderSelection:
                WorldMover.moveSpeed = 0f;
                if (gameOverPanel != null) gameOverPanel.SetActive(false);
                if (deliveryCompletedPanel != null) deliveryCompletedPanel.SetActive(false);
                break;

            case GameState.DeliveryActive:
                WorldMover.moveSpeed = defaultCruiseSpeed;
                if (gameOverPanel != null) gameOverPanel.SetActive(false);
                if (deliveryCompletedPanel != null) deliveryCompletedPanel.SetActive(false);
                OnDeliveryStarted?.Invoke();
                break;

            case GameState.DeliveryCompleted:
                WorldMover.moveSpeed = 0f;
                if (deliveryCompletedPanel != null) deliveryCompletedPanel.SetActive(true);
                OnDeliveryCompleted?.Invoke();
                break;

            case GameState.DeliveryFailed:
                WorldMover.moveSpeed = 0f;
                if (gameOverPanel != null) gameOverPanel.SetActive(true);
                OnDeliveryFailed?.Invoke();
                break;
        }

        OnGameStateChanged?.Invoke(currentState);
    }

    /// <summary>
    /// Transitions from OrderSelection to DeliveryActive and sets cruise speed.
    /// </summary>
    public void StartDelivery()
    {
        if (currentState == GameState.DeliveryActive) return;
        currentScore = 0f;
        SetState(GameState.DeliveryActive);
    }

    /// <summary>
    /// Called when the player successfully reaches the route destination.
    /// </summary>
    public void CompleteDelivery()
    {
        if (currentState != GameState.DeliveryActive) return;
        SetState(GameState.DeliveryCompleted);
    }

    /// <summary>
    /// Called when vehicle health hits 0 or critical time expires.
    /// </summary>
    public void FailDelivery()
    {
        if (currentState == GameState.DeliveryFailed) return;

        if (currentScore > highscore)
        {
            highscore = currentScore;
        }

        SetState(GameState.DeliveryFailed);
    }

    /// <summary>
    /// Legacy alias for FailDelivery() to ensure existing obstacle and player scripts continue to trigger game over.
    /// </summary>
    public void GameOver()
    {
        FailDelivery();
    }

    /// <summary>
    /// Returns the player to the parked vehicle lobby to pick a new order.
    /// </summary>
    public void ReturnToOrderSelection()
    {
        SetState(GameState.OrderSelection);
    }

    public void TogglePause()
    {
        if (currentState == GameState.DeliveryFailed) return;

        isPaused = !isPaused;

        if (pausePanel != null)
        {
            pausePanel.SetActive(isPaused);
        }

        Time.timeScale = isPaused ? 0f : 1f;
        AudioListener.pause = isPaused;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        WorldMover.moveSpeed = defaultCruiseSpeed;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ExitGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        WorldMover.moveSpeed = defaultCruiseSpeed;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
