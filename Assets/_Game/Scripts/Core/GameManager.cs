using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Core State Machine for the DoomDriver gameplay loop.
/// Strictly adheres to Single Responsibility Principle (SRP) and Encapsulation:
/// - Manages GameState transitions and high-level gameplay events.
/// - Does NOT manage economy (see WalletManager).
/// - Does NOT manage world movement speed (see WorldSpeedController).
/// - Does NOT manage pause/timescale (see PauseController).
/// - Does NOT manage score/distance metrics (see DeliveryProgressTracker).
/// - Does NOT manage HUD/UI panels (see GameUIController).
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("State Machine")]
    [SerializeField] private GameState currentState = GameState.OrderSelection;
    public GameState CurrentState => currentState;

    [Header("Scene Navigation")]
    [Tooltip("Name of the scene to load on exit to menu")]
    [SerializeField] private string mainMenuSceneName = "LoadingScreenMenu";

    // High-level C# events for decoupled observation by other systems
    public event System.Action<GameState> OnGameStateChanged;
    public event System.Action OnOrderSelectionStarted;
    public event System.Action OnDeliveryStarted;
    public event System.Action OnDeliveryCompleted;
    public event System.Action OnDeliveryFailed;

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

        EnsureCompanionControllers();
    }

    private void Start()
    {
        ObstacleSpawner.ClearAllObstacles();

        // Start in the parked car lobby
        SetState(GameState.OrderSelection);
    }

    private void Update()
    {
#if UNITY_EDITOR
        HandleDebugInput();
#endif
    }

    /// <summary>
    /// Sets a new game state and triggers the corresponding lifecycle events.
    /// Private to prevent external scripts from forcing arbitrary states.
    /// </summary>
    private void SetState(GameState newState)
    {
        currentState = newState;
        Debug.Log($"[GameManager] Game state changed to: {currentState}");

        switch (currentState)
        {
            case GameState.OrderSelection:
                OnOrderSelectionStarted?.Invoke();
                break;

            case GameState.DeliveryActive:
                OnDeliveryStarted?.Invoke();
                break;

            case GameState.DeliveryCompleted:
                OnDeliveryCompleted?.Invoke();
                break;

            case GameState.DeliveryFailed:
                OnDeliveryFailed?.Invoke();
                break;
        }

        OnGameStateChanged?.Invoke(currentState);
    }

    /// <summary>
    /// Starts a delivery order from the lobby.
    /// </summary>
    public void StartDelivery()
    {
        if (currentState == GameState.DeliveryActive) return;

        ObstacleSpawner.ClearAllObstacles();
        SetState(GameState.DeliveryActive);
    }

    /// <summary>
    /// Successfully finishes the active delivery, pays out reward to WalletManager, and enters completed state.
    /// </summary>
    public void CompleteDelivery()
    {
        if (currentState != GameState.DeliveryActive) return;

        // Reward payout directly into WalletManager (Single Source of Truth)
        if (WalletManager.Instance != null)
        {
            WalletManager.Instance.AddFunds(15f);
        }

        SetState(GameState.DeliveryCompleted);
    }

    /// <summary>
    /// Fails the active delivery (e.g. car destroyed or time expired).
    /// </summary>
    public void FailDelivery()
    {
        if (currentState == GameState.DeliveryFailed) return;

        SetState(GameState.DeliveryFailed);
    }

    /// <summary>
    /// Returns to the parked car lobby to select a new order.
    /// </summary>
    public void ReturnToOrderSelection()
    {
        ObstacleSpawner.ClearAllObstacles();
        SetState(GameState.OrderSelection);
    }

    /// <summary>
    /// Restarts the current active scene.
    /// </summary>
    public void RestartGame()
    {
        if (PauseController.Instance != null)
        {
            PauseController.Instance.SetPaused(false);
        }
        else
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Exits the driving scene and returns to the main menu.
    /// </summary>
    public void ExitGame()
    {
        if (PauseController.Instance != null)
        {
            PauseController.Instance.SetPaused(false);
        }
        else
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>
    /// Ensures all modular core controllers are present on the manager object.
    /// </summary>
    private void EnsureCompanionControllers()
    {
        if (WorldSpeedController.Instance == null && GetComponent<WorldSpeedController>() == null)
        {
            gameObject.AddComponent<WorldSpeedController>();
        }

        if (DeliveryProgressTracker.Instance == null && GetComponent<DeliveryProgressTracker>() == null)
        {
            gameObject.AddComponent<DeliveryProgressTracker>();
        }

        if (PauseController.Instance == null && GetComponent<PauseController>() == null)
        {
            gameObject.AddComponent<PauseController>();
        }

        if (WalletManager.Instance == null && GetComponent<WalletManager>() == null)
        {
            gameObject.AddComponent<WalletManager>();
        }

        if (GameUIController.Instance == null && GetComponent<GameUIController>() == null)
        {
            gameObject.AddComponent<GameUIController>();
        }
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
}
