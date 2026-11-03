using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Presentation only: GameManager owns the immutable result and single settlement.</summary>
public sealed class DeliverySummaryUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup panel;
    [SerializeField] private TMP_Text baseFeeText;
    [SerializeField] private TMP_Text timeTipText;
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private TMP_Text missedTurnsText;
    [SerializeField] private TMP_Text netText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private TMP_Text elapsedTimeText;
    [SerializeField] private Button continueButton;
    [SerializeField, Min(0.01f)] private float fadeSeconds = 0.3f;
    [SerializeField, Min(0.01f)] private float tallySeconds = 1.1f;
    private readonly Dictionary<TMP_Text, decimal> displayedAmounts = new Dictionary<TMP_Text, decimal>();
    private GameManager manager;
    private Coroutine tallyRoutine;
    private bool started;
    private bool showing;
    private bool oldCursorVisible;
    private CursorLockMode oldCursorLock;

    private void Awake() { SetVisible(false); }
    private void Start()
    {
        started = true;
        // Existing scenes normally provide an EventSystem. Create a New Input System fallback only if absent.
        if (EventSystem.current == null && FindFirstObjectByType<EventSystem>() == null)
            new GameObject("Summary EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Bind();
    }
    private void OnEnable() { if (started) Bind(); }
    private void Bind()
    {
        manager = GameManager.Instance;
        if (manager == null) return;
        manager.OnDeliveryCompleted += Show;
        manager.OnGameStateChanged += StateChanged;
        continueButton.onClick.AddListener(Continue);
        StateChanged(manager.CurrentState);
        if (manager.CurrentState == GameState.DeliveryCompleted) Show();
    }
    private void OnDisable()
    {
        if (manager != null)
        {
            manager.OnDeliveryCompleted -= Show;
            manager.OnGameStateChanged -= StateChanged;
        }
        if (continueButton != null) continueButton.onClick.RemoveListener(Continue);
        Hide();
    }
    private void StateChanged(GameState state) { if (state != GameState.DeliveryCompleted) Hide(); }
    private void Show()
    {
        if (manager == null || manager.CompletedDelivery == null || showing) return;
        showing = true;
        oldCursorVisible = Cursor.visible;
        oldCursorLock = Cursor.lockState;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetVisible(true);
        continueButton.interactable = true;
        var result = manager.CompletedDelivery;
        if (elapsedTimeText != null) elapsedTimeText.text = FormatElapsedTime(result.ElapsedSeconds);
        bool capped = result.BaseFee + result.TimeTip < result.DamagePenalty + result.MissedTurnPenalty;
        detailText.gameObject.SetActive(capped);
        detailText.text = capped ? "Minimum earnings: €0.00" : string.Empty;
        tallyRoutine = StartCoroutine(Animate(result));
        continueButton.Select();
    }
    public static string FormatElapsedTime(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds)) seconds = 0;
        int total = (int)System.Math.Min(int.MaxValue, System.Math.Max(0d, System.Math.Floor(seconds)));
        return string.Format(CultureInfo.InvariantCulture, "Time  {0:00}:{1:00}", total / 60, total % 60);
    }

    private IEnumerator Animate(DeliveryResult result)
    {
        float elapsed = 0;
        long previousCents = -1;
        float duration = Mathf.Max(0.01f, tallySeconds);
        while (elapsed < Mathf.Max(duration, fadeSeconds))
        {
            panel.alpha = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeSeconds));
            float t = Mathf.Clamp01(elapsed / duration);
            decimal progress = (decimal)(1f - Mathf.Pow(1f - t, 3f));
            long cents = (long)(progress * 10000m);
            if (cents != previousCents) { Render(result, progress); previousCents = cents; }
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        panel.alpha = 1;
        Render(result, 1m);
        tallyRoutine = null;
    }
    private void Render(DeliveryResult r, decimal t)
    {
        SetMoney(baseFeeText, r.BaseFee * t, "+");
        SetMoney(timeTipText, r.TimeTip * t, "+");
        SetMoney(damageText, r.DamagePenalty * t, "−");
        SetMoney(missedTurnsText, r.MissedTurnPenalty * t, "−");
        SetMoney(netText, r.NetEarnings * t, "");
    }
    private void SetMoney(TMP_Text label, decimal value, string prefix)
    {
        value = System.Math.Round(value, 2, System.MidpointRounding.AwayFromZero);
        if (displayedAmounts.TryGetValue(label, out decimal previous) && previous == value) return;
        displayedAmounts[label] = value;
        string text = prefix + "€" + value.ToString("F2", CultureInfo.InvariantCulture);
        if (label.text != text) label.text = text;
    }
    public void Continue()
    {
        if (manager == null || manager.CurrentState != GameState.DeliveryCompleted) return;
        manager.ReturnToOrderSelection();
    }
    private void Hide()
    {
        if (tallyRoutine != null) StopCoroutine(tallyRoutine);
        tallyRoutine = null;
        if (showing)
        {
            Cursor.lockState = oldCursorLock;
            Cursor.visible = oldCursorVisible;
        }
        showing = false;
        SetVisible(false);
    }
    private void SetVisible(bool visible)
    {
        if (panel == null) return;
        panel.alpha = 0;
        panel.interactable = visible;
        panel.blocksRaycasts = visible;
    }
}
