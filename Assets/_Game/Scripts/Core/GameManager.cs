using UnityEngine;
using UnityEngine.SceneManagement;
using DoomDriver.Economy;

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
    public DeliveryResult CompletedDelivery { get; private set; }
    private DeliveryResult rewardInput;
    private VehicleHealth deliveryHealth;
    private bool rewardSettled;
    private bool returningToLobby;

    [Header("Scene Navigation")]
    [Tooltip("Name of the scene to load on exit to menu")]
    [SerializeField] private string mainMenuSceneName = "LoadingScreenMenu";

    // High-level C# events for decoupled observation by other systems
    public event System.Action<GameState> OnGameStateChanged;
    public event System.Action OnDayBriefingStarted;
    public event System.Action OnOrderSelectionStarted;
    public event System.Action OnDeliveryStarted;
    public event System.Action OnDeliveryCompleted;
    public event System.Action OnDeliveryFailed;
    public event System.Action OnDaySummaryStarted;
    public event System.Action<FailureReason> OnDeliveryFailedWithReason;
    public FailureReason LastFailureReason { get; private set; } = FailureReason.VehicleCrash;

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
        deliveryHealth = FindFirstObjectByType<VehicleHealth>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        ObstacleSpawner.ClearAllObstacles();

        // If a shift was already in progress during session load, resume in OrderSelection.
        // Otherwise, start the day in DayBriefing.
        if (ShiftManager.Instance != null && ShiftManager.Instance.IsShiftActive)
        {
            SetState(GameState.OrderSelection);
        }
        else
        {
            SetState(GameState.DayBriefing);
        }
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
        string shiftInfo = ShiftManager.Instance != null 
            ? $" (Day {ShiftManager.Instance.CurrentDay} • {ShiftManager.Instance.DeliveriesCompletedToday}/{ShiftManager.Instance.DailyQuota} orders • ShiftActive: {ShiftManager.Instance.IsShiftActive})"
            : "";
        Debug.Log($"<color=#60A5FA>[GameManager]</color> 🔄 Game state changed to: <b>{currentState}</b>{shiftInfo}");

        switch (currentState)
        {
            case GameState.DayBriefing:
                OnDayBriefingStarted?.Invoke();
                break;

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
                OnDeliveryFailedWithReason?.Invoke(LastFailureReason);
                break;

            case GameState.DaySummary:
                OnDaySummaryStarted?.Invoke();
                break;
        }

        OnGameStateChanged?.Invoke(currentState);
    }

    /// <summary>
    /// Starts a delivery order from the lobby.
    /// </summary>
    public void StartDelivery()
    {
        if (currentState != GameState.OrderSelection) return;
        CompletedDelivery = null;
        rewardInput = null;
        rewardSettled = false;

        ObstacleSpawner.ClearAllObstacles();
        SetState(GameState.DeliveryActive);
    }

    /// <summary>
    /// Snapshots the successful delivery. Payment occurs only when returning to the lobby.
    /// </summary>
    // Integration point: order/navigation backend supplies final accounting amounts before completion.
    // Never infer financial penalties from the presentation-only GPS source.
    public bool SetDeliveryReward(DeliveryResult result)
    {
        if (currentState != GameState.DeliveryActive || result == null) return false;
        rewardInput = result;
        return true;
    }

    public void CompleteDelivery()
    {
        if (currentState != GameState.DeliveryActive) return;

        decimal missedPenalty = RouteController.Instance != null ? RouteController.Instance.TotalMissedTurnPenalty : 0m;
        var input = rewardInput ?? new DeliveryResult(15m, 0m, 0m, missedPenalty, true);
        if (rewardInput != null && rewardInput.MissedTurnPenalty == 0m && missedPenalty > 0m)
        {
            input = new DeliveryResult(rewardInput.BaseFee, rewardInput.TimeTip, rewardInput.DamagePenalty, missedPenalty, rewardInput.IsPlaceholder);
        }

        CompletedDelivery = input;
        if (PauseController.Instance != null) PauseController.Instance.SetPaused(false);

        SetState(GameState.DeliveryCompleted);
    }

    /// <summary>
    /// Fails the active delivery (e.g. car destroyed or time expired).
    /// </summary>
    public void FailDelivery(FailureReason reason = FailureReason.VehicleCrash)
    {
        if (currentState != GameState.DeliveryActive) return;

        LastFailureReason = reason;

        if (DeliveryPenaltyManager.Instance != null)
        {
            DeliveryPenaltyManager.Instance.ProcessFailure(reason);
        }

        SetState(GameState.DeliveryFailed);
    }

    /// <summary>
    /// Returns to the parked car lobby to select a new order, recording shift progress upon completion.
    /// </summary>
    public void ReturnToOrderSelection()
    {
        if (returningToLobby || currentState == GameState.OrderSelection) return;
        returningToLobby = true;
        try
        {
            if (currentState == GameState.DeliveryCompleted && !rewardSettled)
            {
                if (CompletedDelivery == null || WalletManager.Instance == null)
                {
                    Debug.LogWarning("[Summary] Cannot settle delivery payout: result or wallet is missing. Returning to lobby without payout.");
                }
                else
                {
                    // Set before notifying wallet listeners: reentrant Continue cannot pay twice.
                    rewardSettled = true;
                    WalletManager.Instance.AddFunds((float)CompletedDelivery.NetEarnings);
                    Debug.Log($"<color=#10B981>[GameManager]</color> 💰 Delivery payout settled: +€{CompletedDelivery.NetEarnings:F2}. New Wallet Balance: €{WalletManager.Instance.Balance:F2}");

                    // Record completed delivery in ShiftManager
                    if (ShiftManager.Instance != null)
                    {
                        ShiftManager.Instance.RecordDeliveryCompleted(CompletedDelivery.NetEarnings);
                    }
                }
            }
            if (PauseController.Instance != null) PauseController.Instance.SetPaused(false);
            ObstacleSpawner.ClearAllObstacles();

            // If auto-advance on quota met is active, transition straight to DaySummary
            if (ShiftManager.Instance != null && ShiftManager.Instance.IsQuotaMet && ShiftManager.Instance.AutoAdvanceOnQuotaMet)
            {
                ShiftManager.Instance.EndShift();
                return;
            }

            SetState(GameState.OrderSelection);
        }
        finally { returningToLobby = false; }
    }

    /// <summary>
    /// Transitions from morning briefing to the order selection lobby.
    /// </summary>
    public void TransitionFromBriefingToShift()
    {
        if (currentState != GameState.DayBriefing) return;
        SetState(GameState.OrderSelection);
    }

    /// <summary>
    /// Opens the end-of-day summary and settlement screen.
    /// </summary>
    public void OpenDaySummary()
    {
        if (PauseController.Instance != null) PauseController.Instance.SetPaused(false);
        ObstacleSpawner.ClearAllObstacles();
        SetState(GameState.DaySummary);
    }

    /// <summary>
    /// Starts a new working day morning briefing.
    /// </summary>
    public void StartNewDayBriefing()
    {
        if (PauseController.Instance != null) PauseController.Instance.SetPaused(false);
        ObstacleSpawner.ClearAllObstacles();
        SetState(GameState.DayBriefing);
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

        if (RouteController.Instance == null && GetComponent<RouteController>() == null)
        {
            gameObject.AddComponent<RouteController>();
        }

        if (ShiftManager.Instance == null && GetComponent<ShiftManager>() == null)
        {
            gameObject.AddComponent<ShiftManager>();
        }

        if (DoomDriver.Environment.WeatherManager.Instance == null && GetComponent<DoomDriver.Environment.WeatherManager>() == null)
        {
            gameObject.AddComponent<DoomDriver.Environment.WeatherManager>();
        }

        if (DoomDriver.Core.DailyShiftBriefing.Instance == null && GetComponent<DoomDriver.Core.DailyShiftBriefing>() == null)
        {
            gameObject.AddComponent<DoomDriver.Core.DailyShiftBriefing>();
        }
    }

#if UNITY_EDITOR
    private void HandleDebugInput()
    {
        if (UnityEngine.InputSystem.Keyboard.current == null) return;

        // C: Complete delivery
        if (UnityEngine.InputSystem.Keyboard.current.cKey.wasPressedThisFrame && currentState == GameState.DeliveryActive)
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
        // Up Arrow / W / B: Start Shift from DayBriefing directly into Order Selection
        else if ((UnityEngine.InputSystem.Keyboard.current.upArrowKey.wasPressedThisFrame ||
                  UnityEngine.InputSystem.Keyboard.current.wKey.wasPressedThisFrame ||
                  UnityEngine.InputSystem.Keyboard.current.bKey.wasPressedThisFrame) && currentState == GameState.DayBriefing)
        {
            Debug.Log("<color=green>[DEBUG]</color> Up Arrow / W / B pressed -> Starting Shift and opening Order Selection!");
            if (ShiftManager.Instance != null) ShiftManager.Instance.StartShift();
            else TransitionFromBriefingToShift();

            if (DoomDriver.Phone.PhoneOSController.Instance != null)
            {
                DoomDriver.Phone.PhoneOSController.Instance.OpenScreen(DoomDriver.Phone.PhoneScreen.CourierApp);
            }
        }
        // N: End Shift to DaySummary
        else if (UnityEngine.InputSystem.Keyboard.current.nKey.wasPressedThisFrame && 
                (currentState == GameState.OrderSelection || currentState == GameState.DeliveryCompleted))
        {
            Debug.Log("<color=yellow>[DEBUG]</color> N pressed -> Ending Shift to DaySummary!");
            if (ShiftManager.Instance != null) ShiftManager.Instance.EndShift();
            else OpenDaySummary();
        }
        // M: Advance to Next Day from DaySummary
        else if (UnityEngine.InputSystem.Keyboard.current.mKey.wasPressedThisFrame && currentState == GameState.DaySummary)
        {
            Debug.Log("<color=cyan>[DEBUG]</color> M pressed -> Advancing to Next Day!");
            if (ShiftManager.Instance != null) ShiftManager.Instance.AdvanceToNextDay();
            else StartNewDayBriefing();
        }
        // U: Reset Career back to Day 1 (0 orders)
        else if (UnityEngine.InputSystem.Keyboard.current.uKey.wasPressedThisFrame)
        {
            Debug.Log("<color=red>[DEBUG]</color> U pressed -> Resetting Career to Day 1 (0 orders)!");
            if (ShiftManager.Instance != null) ShiftManager.Instance.ResetCareer();
        }
    }
#endif
}
