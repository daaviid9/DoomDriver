using UnityEngine;

/// <summary>Isolated test-scene driver. Never attached to the production car prefab.</summary>
public sealed class DashboardNavigationDemo : MonoBehaviour
{
    [SerializeField] private DashboardNavigationUI display;
    [SerializeField] private DashboardNavigationSource source;
    [SerializeField] private DashboardNavigationSource.Maneuver direction;
    [SerializeField, Min(0f)] private float remainingMeters = 200f;
    [SerializeField, Min(0f)] private float speed = 12f;
    [SerializeField] private bool autoCountdown = true;
    private int sequence;
    private bool paused;
    private bool active;
    private bool recalculating;
    private string maneuverId;

    private void Awake()
    {
        if (display == null) display = FindFirstObjectByType<DashboardNavigationUI>();
        if (source == null && display != null) source = display.Source;
    }

    private void Start() => NewDelivery();

    private void Update()
    {
        if (!active || paused || recalculating || !autoCountdown) return;
        remainingMeters = Mathf.Max(0f, remainingMeters - speed * Time.deltaTime);
        Publish();
    }

    private void Publish() => source.Publish(maneuverId, direction, remainingMeters);

    [ContextMenu("GPS/1 New delivery (200 m)")]
    public void NewDelivery()
    {
        if (display == null || source == null) return;
        active = true;
        paused = false;
        recalculating = false;
        sequence = 0;
        maneuverId = "demo-0";
        AudioListener.pause = false;
        remainingMeters = 200f;
        display.SetPreviewState(true, false, true);
        Publish();
    }

    [ContextMenu("GPS/2 Apply Inspector distance and direction")]
    public void ApplyValues() { if (source != null) Publish(); }

    [ContextMenu("GPS/3 Next maneuver (200 m)")]
    public void NextManeuver()
    {
        sequence++;
        maneuverId = "demo-" + sequence;
        remainingMeters = 200f;
        direction = (DashboardNavigationSource.Maneuver)(((int)direction + 1) % 3);
        recalculating = false;
        Publish();
    }

    [ContextMenu("GPS/4 Recalculating")]
    public void Recalculate() { recalculating = true; source.SetRecalculating(); }

    [ContextMenu("GPS/5 Toggle pause")]
    public void TogglePause()
    {
        paused = !paused;
        AudioListener.pause = paused;
        display.SetPreviewState(active, paused);
    }

    [ContextMenu("GPS/6 End delivery")]
    public void EndDelivery() { active = false; display.SetPreviewState(false, paused); }

    [ContextMenu("GPS/7 Miss turn (report wrong exit)")]
    public void MissTurn()
    {
        if (!active || source == null) return;
        var wrongExit = direction == DashboardNavigationSource.Maneuver.Left
            ? DashboardNavigationSource.Maneuver.Straight : DashboardNavigationSource.Maneuver.Left;
        if (source.ReportTurnTaken(maneuverId, wrongExit)) recalculating = true;
    }

    [ContextMenu("GPS/8 Blink test (49 m)")]
    public void BlinkTest()
    {
        NewDelivery();
        autoCountdown = false;
        remainingMeters = 49f;
        Publish();
    }

    private void OnDisable() { AudioListener.pause = false; }
}
