using UnityEngine;
using System;

/// <summary>
/// Manages the high-level working day and shift lifecycle.
/// Strictly adheres to Single Responsibility Principle (SRP) and Encapsulation:
/// - Tracks the current working day index (Day 1, Day 2, etc.).
/// - Tracks orders completed during the current shift and evaluates daily quota.
/// - Supports overtime deliveries or early shift completion.
/// - Persists career and day progression across game sessions using PlayerPrefs.
/// - Exposes decoupled C# events for UI, economy, and game systems.
/// </summary>
public class ShiftManager : MonoBehaviour
{
    public static ShiftManager Instance { get; private set; }

    // -------------------------------------------------------------------------
    // PlayerPrefs Storage Keys
    // -------------------------------------------------------------------------
    private const string KEY_CURRENT_DAY = "DoomDriver_CurrentDay";
    private const string KEY_DELIVERIES_TODAY = "DoomDriver_DeliveriesToday";
    private const string KEY_DAILY_QUOTA = "DoomDriver_DailyQuota";
    private const string KEY_CAREER_DELIVERIES = "DoomDriver_CareerDeliveries";
    private const string KEY_SHIFT_EARNINGS = "DoomDriver_ShiftEarnings";
    private const string KEY_IS_SHIFT_ACTIVE = "DoomDriver_IsShiftActive";

    // -------------------------------------------------------------------------
    // Serialized Configuration & State
    // -------------------------------------------------------------------------
    [Header("Shift Progression")]
    [Tooltip("Current working day index (Day 1, Day 2, etc.)")]
    [SerializeField] private int currentDay = 1;

    [Tooltip("Base number of deliveries required to complete Day 1")]
    [SerializeField] private int baseDailyQuota = 3;

    [Tooltip("Additional deliveries added to the quota with each new day")]
    [SerializeField] private int quotaIncrementPerDay = 1;

    [Tooltip("Maximum quota cap to avoid unrealistic shift lengths")]
    [SerializeField] private int maxDailyQuota = 8;

    [Tooltip("Calculated target quota for the current day")]
    [SerializeField] private int dailyQuota = 3;

    [Tooltip("Deliveries successfully completed during the current shift")]
    [SerializeField] private int deliveriesCompletedToday = 0;

    [Tooltip("Total earnings accumulated during the active shift")]
    [SerializeField] private float shiftEarnings = 0f;

    [Tooltip("Career-wide count of all completed deliveries")]
    [SerializeField] private int careerTotalDeliveries = 0;

    [Header("Shift Completion Behavior")]
    [Tooltip("If true, player can continue taking orders even after daily quota is met (overtime)")]
    [SerializeField] private bool allowOvertime = true;

    [Tooltip("If true, automatically advances to DaySummary immediately upon completing quota")]
    [SerializeField] private bool autoAdvanceOnQuotaMet = false;

    [Tooltip("Is a working shift currently active")]
    [SerializeField] private bool isShiftActive = false;

    [Header("Shift Economy")]
    [Tooltip("Daily living bills & vehicle rent deducted at the end of each shift")]
    [SerializeField] private float dailyBills = 25f;

    [Tooltip("Overtime tip bonus percentage unlocked once daily quota is met")]
    [SerializeField] private int overtimeBonusPercent = 25;

    // -------------------------------------------------------------------------
    // Public Properties
    // -------------------------------------------------------------------------
    public int CurrentDay => currentDay;
    public int DailyQuota => dailyQuota;
    public int DeliveriesCompletedToday => deliveriesCompletedToday;
    public float ShiftEarnings => shiftEarnings;
    public float DailyBills => dailyBills;
    public float NetShiftIncome => shiftEarnings - dailyBills;
    public int OvertimeBonusPercent => overtimeBonusPercent;
    public int CareerTotalDeliveries => careerTotalDeliveries;
    public bool AllowOvertime => allowOvertime;
    public bool AutoAdvanceOnQuotaMet
    {
        get => autoAdvanceOnQuotaMet;
        set => autoAdvanceOnQuotaMet = value;
    }
    public bool IsShiftActive => isShiftActive;
    public bool IsQuotaMet => deliveriesCompletedToday >= dailyQuota;
    public int RemainingQuota => Mathf.Max(0, dailyQuota - deliveriesCompletedToday);

    // -------------------------------------------------------------------------
    // Lifecycle Events
    // -------------------------------------------------------------------------
    public static event Action OnShiftStarted;
    public static event Action OnShiftCompleted;
    public static event Action<int> OnDayAdvanced;
    public static event Action<int, int> OnDeliveryCountChanged; // (completedToday, targetQuota)
    public static event Action<float> OnShiftEarningsChanged;

    private bool quotaCompletionEventFired = false;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------
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

        LoadProgress();
        Debug.Log($"<color=#38BDF8>[ShiftManager]</color> 🚀 Loaded Progress -> Day {currentDay} | Quota: {dailyQuota} | Delivered Today: {deliveriesCompletedToday} | Career Deliveries: {careerTotalDeliveries} | Shift Active: {isShiftActive} | Earnings: €{shiftEarnings:F2}");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        // Broadcast initial state to any listeners
        OnDeliveryCountChanged?.Invoke(deliveriesCompletedToday, dailyQuota);
        OnShiftEarningsChanged?.Invoke(shiftEarnings);
    }

    // -------------------------------------------------------------------------
    // Shift Control Methods
    // -------------------------------------------------------------------------

    /// <summary>
    /// Starts the working shift from the morning briefing.
    /// Transitions GameManager into OrderSelection.
    /// </summary>
    public void StartShift()
    {
        isShiftActive = true;
        quotaCompletionEventFired = IsQuotaMet;
        SaveProgress();

        Debug.Log($"<color=#34D399>[ShiftManager]</color> ⏰ SHIFT STARTED for Day {currentDay}! Target Quota: {dailyQuota} deliveries. Clocked In.");
        OnShiftStarted?.Invoke();
        OnDeliveryCountChanged?.Invoke(deliveriesCompletedToday, dailyQuota);

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DayBriefing)
        {
            GameManager.Instance.TransitionFromBriefingToShift();
        }
    }

    /// <summary>
    /// Records an order successfully delivered today.
    /// Increments counters, accumulates earnings, saves progress, and checks quota completion.
    /// </summary>
    /// <param name="earnings">Net earnings paid for this delivery</param>
    public void RecordDeliveryCompleted(decimal earnings)
    {
        deliveriesCompletedToday++;
        careerTotalDeliveries++;
        shiftEarnings += (float)earnings;

        string quotaStatus = IsQuotaMet ? "🏆 <color=#FBBF24>QUOTA MET - OVERTIME ACTIVE (+25% Speed Tip)</color>" : $"({dailyQuota - deliveriesCompletedToday} more needed)";
        Debug.Log($"<color=#34D399>[ShiftManager]</color> 📦 DELIVERY COMPLETED! Today: {deliveriesCompletedToday}/{dailyQuota} {quotaStatus} | Payout: +€{(float)earnings:F2} | Shift Total: €{shiftEarnings:F2}");

        SaveProgress();

        OnDeliveryCountChanged?.Invoke(deliveriesCompletedToday, dailyQuota);
        OnShiftEarningsChanged?.Invoke(shiftEarnings);

        if (IsQuotaMet && !quotaCompletionEventFired)
        {
            quotaCompletionEventFired = true;
            Debug.Log($"<color=#FBBF24>[ShiftManager]</color> 🏆 DAILY QUOTA ACHIEVED ({deliveriesCompletedToday}/{dailyQuota})! Overtime mode unlocked (+25% tip). Player can continue delivering or End Shift.");
            OnShiftCompleted?.Invoke();
        }
    }

    /// <summary>
    /// Ends the active shift and opens the DaySummary screen.
    /// Can be triggered automatically when quota is met, or manually by the player.
    /// </summary>
    public void EndShift()
    {
        // Guard against duplicate settlement if already in DaySummary
        if (!isShiftActive && GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DaySummary)
        {
            return;
        }

        Debug.Log($"<color=#F87171>[ShiftManager]</color> 🏁 SHIFT ENDED for Day {currentDay}. Finished {deliveriesCompletedToday} orders today with €{shiftEarnings:F2} earned. Deducting €{dailyBills:F2} daily bills.");
        isShiftActive = false;

        if (WalletManager.Instance != null && dailyBills > 0f)
        {
            WalletManager.Instance.DeductFunds(dailyBills);
        }

        SaveProgress();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OpenDaySummary();
        }
    }

    /// <summary>
    /// Advances to the next working day, resetting daily counters while preserving career stats.
    /// Transitions GameManager to DayBriefing for the new day.
    /// </summary>
    public void AdvanceToNextDay()
    {
        currentDay++;
        deliveriesCompletedToday = 0;
        shiftEarnings = 0f;
        isShiftActive = false;
        quotaCompletionEventFired = false;
        dailyQuota = CalculateDailyQuota(currentDay);

        SaveProgress();

        Debug.Log($"<color=#A78BFA>[ShiftManager]</color> ☀️ ADVANCED TO NEXT DAY: Day {currentDay}. New target quota: {dailyQuota} orders. Daily counters reset to 0.");

        OnDayAdvanced?.Invoke(currentDay);
        OnDeliveryCountChanged?.Invoke(deliveriesCompletedToday, dailyQuota);
        OnShiftEarningsChanged?.Invoke(shiftEarnings);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartNewDayBriefing();
        }
    }

    /// <summary>
    /// Dynamically calculates quota based on day number.
    /// </summary>
    public int CalculateDailyQuota(int dayIndex)
    {
        int scaled = baseDailyQuota + Mathf.Max(0, dayIndex - 1) * quotaIncrementPerDay;
        return Mathf.Clamp(scaled, baseDailyQuota, maxDailyQuota);
    }

    // -------------------------------------------------------------------------
    // PlayerPrefs Persistence
    // -------------------------------------------------------------------------

    /// <summary>
    /// Saves all shift and career metrics to PlayerPrefs.
    /// </summary>
    public void SaveProgress()
    {
        PlayerPrefs.SetInt(KEY_CURRENT_DAY, currentDay);
        PlayerPrefs.SetInt(KEY_DELIVERIES_TODAY, deliveriesCompletedToday);
        PlayerPrefs.SetInt(KEY_DAILY_QUOTA, dailyQuota);
        PlayerPrefs.SetInt(KEY_CAREER_DELIVERIES, careerTotalDeliveries);
        PlayerPrefs.SetFloat(KEY_SHIFT_EARNINGS, shiftEarnings);
        PlayerPrefs.SetInt(KEY_IS_SHIFT_ACTIVE, isShiftActive ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Loads saved metrics from PlayerPrefs or initialises defaults.
    /// </summary>
    public void LoadProgress()
    {
        currentDay = PlayerPrefs.GetInt(KEY_CURRENT_DAY, 1);
        if (currentDay < 1) currentDay = 1;

        dailyQuota = PlayerPrefs.GetInt(KEY_DAILY_QUOTA, CalculateDailyQuota(currentDay));
        deliveriesCompletedToday = PlayerPrefs.GetInt(KEY_DELIVERIES_TODAY, 0);
        careerTotalDeliveries = PlayerPrefs.GetInt(KEY_CAREER_DELIVERIES, 0);
        shiftEarnings = PlayerPrefs.GetFloat(KEY_SHIFT_EARNINGS, 0f);
        isShiftActive = PlayerPrefs.GetInt(KEY_IS_SHIFT_ACTIVE, 0) == 1;

        quotaCompletionEventFired = IsQuotaMet;
    }

    /// <summary>
    /// Resets all career progress back to Day 1 defaults (useful for testing or New Game).
    /// </summary>
    [ContextMenu("Reset Career (Day 1, 0 Orders)")]
    public void ResetCareer()
    {
        PlayerPrefs.DeleteKey(KEY_CURRENT_DAY);
        PlayerPrefs.DeleteKey(KEY_DELIVERIES_TODAY);
        PlayerPrefs.DeleteKey(KEY_DAILY_QUOTA);
        PlayerPrefs.DeleteKey(KEY_CAREER_DELIVERIES);
        PlayerPrefs.DeleteKey(KEY_SHIFT_EARNINGS);
        PlayerPrefs.DeleteKey(KEY_IS_SHIFT_ACTIVE);
        PlayerPrefs.Save();

        currentDay = 1;
        dailyQuota = CalculateDailyQuota(1);
        deliveriesCompletedToday = 0;
        careerTotalDeliveries = 0;
        shiftEarnings = 0f;
        isShiftActive = false;
        quotaCompletionEventFired = false;

        Debug.Log("<color=#EF4444>[ShiftManager]</color> ⚠️ Career progress reset to Day 1 defaults (Quota: 3, 0 orders, €0.00).");

        OnDayAdvanced?.Invoke(currentDay);
        OnDeliveryCountChanged?.Invoke(deliveriesCompletedToday, dailyQuota);
        OnShiftEarningsChanged?.Invoke(shiftEarnings);
    }
}
