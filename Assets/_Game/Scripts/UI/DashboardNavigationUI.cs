using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class DashboardNavigationUI : MonoBehaviour
{
    [SerializeField] private DashboardNavigationSource source;
    [SerializeField] private TMP_Text distanceText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private RectTransform arrow;
    [SerializeField] private AudioSource warningAudio;
    [SerializeField] private AudioClip warningClip;
    [Header("Spoken navigation")]
    [SerializeField] private bool useVoiceGuidance = true;
    [SerializeField] private AudioClip turnLeftVoice;
    [SerializeField] private AudioClip turnRightVoice;
    [SerializeField] private AudioClip straightVoice;
    [SerializeField] private AudioClip recalculatingVoice;
    [SerializeField, Min(0f)] private float warningDistance = 70f;
    [SerializeField] private Color normalColor = new Color(0.87f, 0.9f, 0.82f);
    [SerializeField] private Color recalculatingColor = new Color(1f, 0.55f, 0.12f);

    private readonly DashboardWarningGate warningGate = new DashboardWarningGate();
    private readonly DashboardArrowBlink arrowBlink = new DashboardArrowBlink();
    private string blinkManeuverId;
    private GameManager gameManager;
    private PauseController pauseController;
    private bool initialized;
    private bool previewActive;
    private bool previewPaused;
    private int lastMeters = -1;
    private bool recalculationAnnounced;
    private string audioManeuverId;

    public DashboardNavigationSource Source => source;
    public int WarningCount => warningGate.Count;
    private bool Active => gameManager != null
        ? gameManager.CurrentState == GameState.DeliveryActive : previewActive;
    private bool Paused => previewPaused || (pauseController != null && pauseController.IsPaused);

    private void Start()
    {
        gameManager = GameManager.Instance;
        pauseController = PauseController.Instance;
        initialized = true;
        Subscribe();
        Refresh();
    }

    private void Update() => UpdateArrowVisibility(Time.unscaledDeltaTime);

    private void UpdateArrowVisibility(float deltaTime)
    {
        if (arrow == null) return;
        bool navigating = Active && source != null && source.State == DashboardNavigationSource.NavigationState.Navigating;
        string id = navigating ? source.ManeuverId : null;
        if (id != blinkManeuverId) { arrowBlink.Reset(); blinkManeuverId = id; }
        bool visible = arrowBlink.Step(navigating && source.RemainingMeters > 0f && source.RemainingMeters < 50f, Paused, deltaTime);
        bool show = navigating && visible;
        if (arrow.gameObject.activeSelf != show) arrow.gameObject.SetActive(show);
    }

    private void OnEnable()
    {
        if (!initialized) return;
        if (source != null) source.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (source != null) source.Changed -= Refresh;
        if (warningAudio != null) warningAudio.Stop();
    }

    private void OnDestroy()
    {
        // Keep lifecycle subscriptions while hidden so a new delivery still resets warning IDs.
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= HandleState;
            gameManager.OnOrderSelectionStarted -= ResetDelivery;
        }
        if (pauseController != null) pauseController.OnPauseToggled -= HandlePause;
    }

    private void Subscribe()
    {
        if (source != null) source.Changed += Refresh;
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged += HandleState;
            gameManager.OnOrderSelectionStarted += ResetDelivery;
        }
        if (pauseController != null) pauseController.OnPauseToggled += HandlePause;
    }

    private void HandleState(GameState state)
    {
        if (state != GameState.DeliveryActive && warningAudio != null) warningAudio.Stop();
        Refresh();
    }

    private void HandlePause(bool paused) => Refresh();

    private void ResetDelivery()
    {
        warningGate.Reset();
        arrowBlink.Reset();
        blinkManeuverId = null;
        recalculationAnnounced = false;
        audioManeuverId = null;
        if (warningAudio != null) warningAudio.Stop();
        if (source != null) source.ClearRoute();
        Refresh();
    }

    // Used only by the isolated preview scene (which has no GameManager).
    public void SetPreviewState(bool active, bool paused, bool reset = false)
    {
        if (gameManager != null) return;
        previewActive = active;
        previewPaused = paused;
        if (reset) ResetDelivery();
        Refresh();
    }

    private void Refresh()
    {
        if (!isActiveAndEnabled) return;
        bool navigating = Active && source != null && source.State == DashboardNavigationSource.NavigationState.Navigating;
        bool recalculating = Active && source != null && source.State == DashboardNavigationSource.NavigationState.Recalculating;
        if (arrow != null)
        {
            UpdateArrowVisibility(0f);
            if (navigating)
            {
                float angle = source.Direction == DashboardNavigationSource.Maneuver.Left ? 90f
                    : source.Direction == DashboardNavigationSource.Maneuver.Right ? -90f : 0f;
                arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        if (statusText != null)
        {
            string label = recalculating ? "Recalculating..." : !navigating ? "Waiting for route"
                : source.Direction == DashboardNavigationSource.Maneuver.Left ? "Turn left"
                : source.Direction == DashboardNavigationSource.Maneuver.Right ? "Turn right" : "Continue straight";
            if (statusText.text != label) statusText.text = label;
            statusText.color = recalculating ? recalculatingColor : normalColor;
        }

        int meters = navigating ? Mathf.CeilToInt(source.RemainingMeters) : -1;
        if (distanceText != null && (meters != lastMeters || string.IsNullOrEmpty(distanceText.text)))
        {
            if (navigating) distanceText.SetText("{0} m", meters);
            else distanceText.text = "--";
        }
        lastMeters = meters;

        if (!navigating)
        {
            if (!recalculating)
            {
                if (warningAudio != null) warningAudio.Stop();
                recalculationAnnounced = false;
                audioManeuverId = null;
            }
            else if (!recalculationAnnounced)
            {
                if (warningAudio != null) warningAudio.Stop();
                if (!Paused && warningAudio != null)
                {
                    AudioClip recalcClip = (useVoiceGuidance && recalculatingVoice != null) ? recalculatingVoice : warningClip;
                    if (recalcClip != null)
                    {
                        warningAudio.PlayOneShot(recalcClip);
                        recalculationAnnounced = true;
                    }
                }
            }
            return;
        }

        if (recalculationAnnounced || audioManeuverId != source.ManeuverId)
        {
            if (warningAudio != null) warningAudio.Stop();
            recalculationAnnounced = false;
            audioManeuverId = source.ManeuverId;
        }
        AudioClip clip = GetManeuverClip(source.Direction);
        if (warningGate.TryConsume(source.ManeuverId, source.RemainingMeters, warningDistance,
            !Paused && warningAudio != null && clip != null))
        {
            warningAudio.PlayOneShot(clip);
        }
    }

    public AudioClip GetManeuverClip(DashboardNavigationSource.Maneuver direction)
    {
        if (!useVoiceGuidance) return warningClip;
        AudioClip voice = direction == DashboardNavigationSource.Maneuver.Left ? turnLeftVoice
            : direction == DashboardNavigationSource.Maneuver.Right ? turnRightVoice : straightVoice;
        return voice != null ? voice : warningClip;
    }
}

/// <summary>One warning per maneuver per delivery, independent of view enable/disable.</summary>
public sealed class DashboardWarningGate
{
    private readonly HashSet<string> warned = new HashSet<string>();
    public int Count => warned.Count;

    public bool TryConsume(string id, float meters, float threshold, bool eligible)
    {
        return eligible && !string.IsNullOrEmpty(id) && meters > 0f && meters <= threshold && warned.Add(id);
    }

    public void Reset() => warned.Clear();
}

/// <summary>Slow 1.25 Hz blink below 50 m. Pausing freezes the phase; leaving the zone restores visibility.</summary>
public sealed class DashboardArrowBlink
{
    private float phase;
    public bool Step(bool eligible, bool paused, float deltaTime)
    {
        if (!eligible) { Reset(); return true; }
        if (!paused) phase = (phase + Mathf.Max(0f, deltaTime)) % 0.8f;
        return phase < 0.4f;
    }
    public void Reset() => phase = 0f;
}