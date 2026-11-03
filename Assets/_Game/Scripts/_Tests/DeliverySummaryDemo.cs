using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Only for DeliverySummaryTest. Exercises the real GameManager, UI and in-memory wallet.</summary>
public sealed class DeliverySummaryDemo : MonoBehaviour
{
    [Header("Custom accounting input (demo euros)")]
    [SerializeField, Min(0)] private float baseFee = 15;
    [SerializeField, Min(0)] private float timeTip = 3.5f;
    [SerializeField, Min(0)] private float damagePenalty = 2;
    [SerializeField, Min(0)] private float missedTurnPenalty = 1;
    [Header("Live test status")]
    [SerializeField] private string state;
    [SerializeField] private float walletBalance;
    [SerializeField] private string lastCheck = "Ready";
    private GameManager manager;
    private WalletManager wallet;
    private VehicleHealth health;
    private DeliverySummaryUI display;
    private CanvasGroup panel;
    private TMP_Text net;
    private TMP_Text status;
    private Button button;
    private bool ready;
    private bool runningChecks;

    private void Awake()
    {
        manager = GetComponent<GameManager>();
        wallet = GetComponent<WalletManager>();
        health = GetComponent<VehicleHealth>();
        display = FindFirstObjectByType<DeliverySummaryUI>();
        if (wallet == null || wallet.PersistsBalance || display == null || manager == null || health == null)
        {
            Debug.LogError("[Summary demo] Use DeliverySummaryTest with its non-persistent wallet.");
            enabled = false;
            return;
        }
        panel = display.GetComponent<CanvasGroup>();
        net = display.transform.Find("Receipt/Net amount").GetComponent<TMP_Text>();
        button = display.transform.Find("Receipt/Continue").GetComponent<Button>();
        CreateStatus();
    }

    private IEnumerator Start()
    {
        // Wait for real components to finish their Start subscriptions and initial lobby transition.
        yield return null;
        ready = true;
        manager.OnGameStateChanged += StateChanged;
        WalletManager.OnBalanceChanged += BalanceChanged;
        ShowSample();
    }

    private void OnDestroy()
    {
        if (manager != null) manager.OnGameStateChanged -= StateChanged;
        WalletManager.OnBalanceChanged -= BalanceChanged;
        Time.timeScale = 1;
        AudioListener.pause = false;
    }

    private bool CanRun() => Application.isPlaying && ready && !runningChecks;
    private void StateChanged(GameState value) => RefreshStatus();
    private void BalanceChanged(float value) => RefreshStatus();
    private void RefreshStatus()
    {
        state = manager.CurrentState.ToString();
        walletBalance = wallet.Balance;
        status.text = $"TEST\nWallet: €{walletBalance:F2}\n{state}\n\n{lastCheck}";
    }
    private void Note(string message) { lastCheck = message; RefreshStatus(); }
    private void CreateStatus()
    {
        var root = new GameObject("Summary Test Instructions", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 90;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var go = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(root.transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(36, -60);
        rect.sizeDelta = new Vector2(450, 900);
        status = go.GetComponent<TMP_Text>();
        status.font = TMP_Settings.defaultFontAsset;
        status.fontSize = 22;
        status.raycastTarget = false;
        status.color = new Color(0.8f, 0.9f, 0.85f);
    }
    private void Begin()
    {
        if (manager.CurrentState != GameState.OrderSelection) manager.ReturnToOrderSelection();
        manager.StartDelivery();
    }
    private void Complete(DeliveryResult result)
    {
        Begin();
        health.TakeDamage(20);
        manager.SetDeliveryReward(result);
        manager.CompleteDelivery();
    }

    [ContextMenu("Summary/1 Show sample (15.50 EUR)")]
    public void ShowSample()
    {
        if (!CanRun()) return;
        Complete(new DeliveryResult(15m, 3.5m, 2m, 1m));
        Note("Expected: €15.50\nContinue adds it once.");
    }
    [ContextMenu("Summary/2 Show fallback (15 EUR)")]
    public void ShowFallback()
    {
        if (!CanRun()) return;
        Begin();
        manager.CompleteDelivery();
        Note("Expected: €15.00\nOther amounts are zero.");
    }
    [ContextMenu("Summary/3 Show high penalties (0 EUR)")]
    public void ShowZero()
    {
        if (!CanRun()) return;
        Complete(new DeliveryResult(15m, 0m, 20m, 5m));
        Note("Expected: €0.00\nNo wallet deduction.");
    }
    [ContextMenu("Summary/4 Apply Inspector amounts")]
    public void ApplyValues()
    {
        if (!CanRun()) return;
        try
        {
            var result = new DeliveryResult((decimal)baseFee, (decimal)timeTip, (decimal)damagePenalty, (decimal)missedTurnPenalty);
            Complete(result);
            Note($"Custom expected: €{result.NetEarnings:F2}");
        }
        catch (Exception error) { Debug.LogWarning("[Summary demo] Invalid amounts: " + error.Message); }
    }
    [ContextMenu("Summary/5 Double Continue")]
    public void DoubleContinue()
    {
        if (!CanRun() || manager.CurrentState != GameState.DeliveryCompleted) return;
        float before = wallet.Balance;
        float amount = (float)manager.CompletedDelivery.NetEarnings;
        button.onClick.Invoke();
        button.onClick.Invoke();
        bool pass = Mathf.Abs(wallet.Balance - before - amount) < 0.001f && manager.CurrentState == GameState.OrderSelection;
        Note(pass ? "PASS: paid once, lobby." : "FAIL: duplicate payment/state.");
    }
    [ContextMenu("Summary/6 Reopen panel (no payment)")]
    public void Reopen()
    {
        if (!CanRun() || manager.CurrentState != GameState.DeliveryCompleted) return;
        float before = wallet.Balance;
        display.enabled = false;
        display.enabled = true;
        Note(wallet.Balance == before ? "PASS: reopen did not pay." : "FAIL: reopen paid.");
    }
    [ContextMenu("Summary/7 Toggle pause")]
    public void TogglePause()
    {
        if (!CanRun() || PauseController.Instance == null) return;
        PauseController.Instance.SetPaused(!PauseController.Instance.IsPaused);
        Note(PauseController.Instance.IsPaused ? "Paused. Tally still runs." : "Resumed.");
    }
    [ContextMenu("Summary/8 Fail delivery (no summary)")]
    public void Fail()
    {
        if (!CanRun()) return;
        Begin();
        float before = wallet.Balance;
        manager.FailDelivery();
        Note(wallet.Balance == before && panel.alpha == 0 ? "PASS: failure, no payout/UI." : "FAIL: failure handling.");
    }
    [ContextMenu("Summary/9 Run automatic checks")]
    public void RunChecks()
    {
        if (!CanRun()) return;
        StartCoroutine(Checks());
    }
    [ContextMenu("Summary/10 Timed delivery (3 seconds)")]
    public void TimedDelivery()
    {
        if (!CanRun()) return;
        StartCoroutine(TimedDeliveryRoutine());
    }
    private IEnumerator TimedDeliveryRoutine()
    {
        runningChecks = true;
        var tracker = GetComponent<DeliveryProgressTracker>();
        // WorldSpeedController stays disabled: distance remains zero, so no highscore is written.
        tracker.enabled = true;
        yield return null;
        Begin();
        if (PauseController.Instance != null) PauseController.Instance.SetPaused(false);
        Note("Driving for 3 seconds...");
        yield return new WaitForSeconds(3f);
        manager.SetDeliveryReward(new DeliveryResult(15m, 3.5m, 2m, 1m));
        manager.CompleteDelivery();
        tracker.enabled = false;
        runningChecks = false;
        Note("Expected time: about 00:03");
    }

    private void Require(bool passed, string name)
    {
        if (!passed) throw new InvalidOperationException(name);
        Debug.Log("[Summary test] PASS: " + name);
    }
    private IEnumerator Checks()
    {
        runningChecks = true;
        var checks = CheckSteps();
        bool failed = false;
        while (true)
        {
            bool next;
            try { next = checks.MoveNext(); }
            catch (Exception error)
            {
                Note("FAIL: " + error.Message);
                Debug.LogException(error);
                failed = true;
                break;
            }
            if (!next) break;
            yield return checks.Current;
        }
        (checks as IDisposable)?.Dispose();
        if (PauseController.Instance != null) PauseController.Instance.SetPaused(false);
        runningChecks = false;
        if (!failed) Note("ALL CHECKS PASSED\nSample is ready again.");
    }
    private IEnumerator CheckSteps()
    {
        Begin();
        float before = wallet.Balance;
        health.TakeDamage(20);
        manager.SetDeliveryReward(new DeliveryResult(15m, 3.5m, 2m, 1m));
        manager.CompleteDelivery();
        manager.CompleteDelivery();
        Require(wallet.Balance == before, "completion does not pay");
        Require(manager.CompletedDelivery.DamageHp == 20, "damage captured before lobby reset");
        manager.StartDelivery();
        Require(manager.CurrentState == GameState.DeliveryCompleted, "new delivery cannot bypass settlement");
        display.enabled = false;
        display.enabled = true;
        Require(wallet.Balance == before, "reopening does not pay");
        PauseController.Instance.SetPaused(true);
        yield return new WaitForSecondsRealtime(1.5f);
        Require(net.text == "€15.50" && panel.alpha == 1, "exact tally and fade while paused");
        var snapshot = manager.CompletedDelivery;
        button.onClick.Invoke();
        button.onClick.Invoke();
        Require(Mathf.Abs(wallet.Balance - before - 15.5f) < 0.001f, "double Continue pays once");
        Require(manager.CurrentState == GameState.OrderSelection && panel.alpha == 0 && !panel.blocksRaycasts, "Continue hides panel and returns to lobby");
        Require(!PauseController.Instance.IsPaused && health.CurrentHealth == health.MaxHealth && snapshot.DamageHp == 20, "unpause, health reset and stable snapshot");
        Complete(new DeliveryResult(15, 0, 20, 5));
        before = wallet.Balance;
        button.onClick.Invoke();
        Require(wallet.Balance == before, "early Continue and zero floor");
        Begin();
        manager.CompleteDelivery();
        Require(manager.CompletedDelivery.IsPlaceholder && manager.CompletedDelivery.NetEarnings == 15, "next delivery resets accounting input");
        before = wallet.Balance;
        // Same path as the editor R shortcut, including a reentrant wallet listener.
        Action<float> reenter = ignored => manager.ReturnToOrderSelection();
        WalletManager.OnBalanceChanged += reenter;
        try { manager.ReturnToOrderSelection(); manager.ReturnToOrderSelection(); }
        finally { WalletManager.OnBalanceChanged -= reenter; }
        Require(Mathf.Abs(wallet.Balance - before - 15) < 0.001f, "R path/reentrant callback pays once");
        Begin();
        before = wallet.Balance;
        manager.FailDelivery();
        manager.CompleteDelivery();
        Require(wallet.Balance == before && panel.alpha == 0 && manager.CurrentState == GameState.DeliveryFailed, "failure never shows/pays success");
        var tracker = GetComponent<DeliveryProgressTracker>();
        tracker.enabled = true;
        yield return null;
        Begin();
        yield return new WaitForSeconds(1.1f);
        manager.SetDeliveryReward(new DeliveryResult(15m, 3.5m, 2m, 1m));
        manager.CompleteDelivery();
        tracker.enabled = false;
        var elapsedLabel = display.transform.Find("Receipt/Delivery time").GetComponent<TMP_Text>();
        Require(manager.CompletedDelivery.ElapsedSeconds >= 1f && elapsedLabel.text == DeliverySummaryUI.FormatElapsedTime(manager.CompletedDelivery.ElapsedSeconds), "real elapsed time appears in summary");
    }
}
