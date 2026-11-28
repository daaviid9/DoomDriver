using UnityEngine;
using UnityEngine.SceneManagement;
using DoomDriver.Economy;

/// <summary>
/// Core State Machine for the DoomDriver gameplay loop.
/// Manages GameState transitions, delivery lifecycle, and scene navigation.
/// Companion managers (ShiftManager, WalletManager, UpgradeManager, WeatherManager, etc.)
/// must be placed explicitly as scene objects/prefabs rather than auto-added here.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("State Machine")]
    [SerializeField] private GameState currentState = GameState.OrderSelection;
    public GameState CurrentState => currentState;
    public DeliveryResult CompletedDelivery { get; private set; }
    private DeliveryResult rewardInput;
    private bool rewardSettled;
    private bool returningToLobby;

    [Header("Scene Navigation")]
    [Tooltip("Name of the scene to load on exit to menu")]
    [SerializeField] private string mainMenuSceneName = "LoadingScreenMenu";

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
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        ObstacleSpawner.ClearAllObstacles();

        if (ShiftManager.Instance != null && ShiftManager.Instance.IsShiftActive)
        {
            SetState(GameState.OrderSelection);
        }
        else
        {
            SetState(GameState.DayBriefing);
        }
    }

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

    public void StartDelivery()
    {
        if (currentState != GameState.OrderSelection) return;
        CompletedDelivery = null;
        rewardInput = null;
        rewardSettled = false;

        ObstacleSpawner.ClearAllObstacles();
        SetState(GameState.DeliveryActive);
    }

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
                    rewardSettled = true;
                    WalletManager.Instance.AddFunds((float)CompletedDelivery.NetEarnings);
                    Debug.Log($"<color=#10B981>[GameManager]</color> 💰 Delivery payout settled: +€{CompletedDelivery.NetEarnings:F2}. New Wallet Balance: €{WalletManager.Instance.Balance:F2}");

                    if (ShiftManager.Instance != null)
                    {
                        ShiftManager.Instance.RecordDeliveryCompleted(CompletedDelivery.NetEarnings);
                    }
                }
            }

            if (PauseController.Instance != null) PauseController.Instance.SetPaused(false);
            ObstacleSpawner.ClearAllObstacles();

            if (ShiftManager.Instance != null && ShiftManager.Instance.IsQuotaMet && ShiftManager.Instance.AutoAdvanceOnQuotaMet)
            {
                ShiftManager.Instance.EndShift();
                return;
            }

            SetState(GameState.OrderSelection);
        }
        finally
        {
            returningToLobby = false;
        }
    }

    public void TransitionFromBriefingToShift()
    {
        if (currentState != GameState.DayBriefing) return;
        SetState(GameState.OrderSelection);
    }

    public void OpenDaySummary()
    {
        if (PauseController.Instance != null) PauseController.Instance.SetPaused(false);
        ObstacleSpawner.ClearAllObstacles();
        SetState(GameState.DaySummary);
    }

    public void StartNewDayBriefing()
    {
        if (PauseController.Instance != null) PauseController.Instance.SetPaused(false);
        ObstacleSpawner.ClearAllObstacles();
        SetState(GameState.DayBriefing);
    }

    public void RestartGame()
    {
        ResetPauseAndTimeScale();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ExitGame()
    {
        ResetPauseAndTimeScale();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        ResetPauseAndTimeScale();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void ResetPauseAndTimeScale()
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
    }
}
