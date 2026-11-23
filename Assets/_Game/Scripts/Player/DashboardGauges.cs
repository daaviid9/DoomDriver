using UnityEngine;

/// <summary>
/// Animates the analog speedometer and tachometer (RPM) needles on the 3D car cockpit instrument cluster.
/// Maps WorldMover / WorldSpeedController movement speed to the speedometer needle (0 to 180 km/h)
/// and engine audio pitch / throttle ratio to the RPM tachometer needle.
/// Adds subtle high-speed jitter/vibration for realistic mechanical feedback.
/// </summary>
public class DashboardGauges : MonoBehaviour
{
    public static DashboardGauges Instance { get; private set; }

    [Header("Speedometer (Speed Gauge)")]
    [Tooltip("Transform of the speedometer needle")]
    [SerializeField] private Transform speedometerNeedle;

    [Tooltip("Maximum display speed in km/h representing the top of the gauge dial (typically 180 km/h)")]
    [SerializeField] private float maxDisplaySpeedKmh = 180f;

    [Tooltip("Needle angle (in degrees) when speed is 0 km/h (bottom-left at 7 o'clock)")]
    [SerializeField] private float speedZeroAngle = -135f;

    [Tooltip("Needle angle (in degrees) when speed is maxDisplaySpeedKmh (bottom-right at 5 o'clock)")]
    [SerializeField] private float speedMaxAngle = 135f;

    [Tooltip("Local axis around which the speedometer needle rotates. Face normal into dial = (0, -1, 0)")]
    [SerializeField] private Vector3 speedRotationAxis = new Vector3(0f, -1f, 0f);

    [Header("Tachometer (RPM Gauge)")]
    [Tooltip("Transform of the tachometer / RPM needle")]
    [SerializeField] private Transform tachometerNeedle;

    [Tooltip("Idle RPM value (gauge rest state)")]
    [SerializeField] private float idleRpm = 900f;

    [Tooltip("Maximum redline RPM on the gauge dial (typically 7000 or 8000 RPM)")]
    [SerializeField] private float maxRpm = 7000f;

    [Tooltip("Needle angle (in degrees) when RPM is 0 (bottom-left at 7 o'clock)")]
    [SerializeField] private float rpmZeroAngle = -135f;

    [Tooltip("Needle angle (in degrees) when RPM is at max/redline (bottom-right at 5 o'clock)")]
    [SerializeField] private float rpmMaxAngle = 135f;

    [Tooltip("Local axis around which the tachometer needle rotates. Face normal into dial = (0, -1, 0)")]
    [SerializeField] private Vector3 rpmRotationAxis = new Vector3(0f, -1f, 0f);

    [Header("Dynamics & Smoothing")]
    [Tooltip("Smoothing speed for needle movement (higher = snappier, lower = smoother lag)")]
    [SerializeField] private float needleSmoothing = 14f;

    [Header("High-Speed Needle Vibration / Jitter")]
    [Tooltip("Speed in km/h above which the speedometer needle starts vibrating subtely")]
    [SerializeField] private float vibrationThresholdKmh = 60f;

    [Tooltip("Maximum amplitude of jitter oscillation in degrees at max speed")]
    [SerializeField] private float maxJitterAngle = 1.25f;

    [Tooltip("Frequency of needle vibration")]
    [SerializeField] private float jitterFrequency = 32f;

    [Header("Manual Calibration / Testing")]
    [Tooltip("When enabled, overrides automatic speed/RPM with test values below")]
    [SerializeField] private bool manualTestMode = false;

    [Range(0f, 180f)]
    [Tooltip("Manual speed in km/h for testing in Inspector")]
    [SerializeField] private float testSpeedKmh = 0f;

    [Range(0f, 7000f)]
    [Tooltip("Manual RPM for testing in Inspector")]
    [SerializeField] private float testRpm = 900f;

    // Runtime state
    private float currentDisplayedKmh = 0f;
    private float currentDisplayedRpm = 900f;
    private Quaternion speedInitialLocalRot = Quaternion.identity;
    private Quaternion rpmInitialLocalRot = Quaternion.identity;

    // Cached references
    private AudioSource cachedEngineAudio;

    public float CurrentKmh => currentDisplayedKmh;
    public float CurrentRpm => currentDisplayedRpm;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        if (speedometerNeedle != null)
        {
            speedInitialLocalRot = speedometerNeedle.localRotation;
        }

        if (tachometerNeedle != null)
        {
            rpmInitialLocalRot = tachometerNeedle.localRotation;
        }

        currentDisplayedRpm = idleRpm;
    }

    private void Start()
    {
        LocateNeedlesIfUnassigned();
        FindEngineAudioSource();
    }

    public void LocateNeedlesIfUnassigned()
    {
        if (speedometerNeedle == null || tachometerNeedle == null)
        {
            Transform clocksRoot = transform.Find("Car/Clocks") ?? transform.Find("Clocks");
            if (clocksRoot == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null)
                {
                    clocksRoot = p.transform.Find("Car/Clocks") ?? p.transform.Find("Clocks");
                }
            }

            if (clocksRoot != null)
            {
                if (speedometerNeedle == null)
                {
                    var needle = clocksRoot.Find("Clock 2/SpeedNeedle") ?? clocksRoot.Find("SpeedNeedle") ?? clocksRoot.Find("Clock 2");
                    if (needle != null)
                    {
                        speedometerNeedle = needle;
                        speedInitialLocalRot = needle.localRotation;
                    }
                }

                if (tachometerNeedle == null)
                {
                    var needle = clocksRoot.Find("Clock 1/RpmNeedle") ?? clocksRoot.Find("RpmNeedle") ?? clocksRoot.Find("Clock 1");
                    if (needle != null)
                    {
                        tachometerNeedle = needle;
                        rpmInitialLocalRot = needle.localRotation;
                    }
                }
            }
        }
    }

    private void FindEngineAudioSource()
    {
        var vehicleAudio = FindFirstObjectByType<VehicleAudio>();
        if (vehicleAudio != null)
        {
            cachedEngineAudio = vehicleAudio.GetComponent<AudioSource>();
        }
    }

    private void Update()
    {
        float targetKmh = 0f;
        float targetRpm = idleRpm;

        if (manualTestMode)
        {
            targetKmh = testSpeedKmh;
            targetRpm = testRpm;
        }
        else
        {
            float targetSpeedMps = 0f;
            if (WorldSpeedController.Instance != null)
            {
                targetSpeedMps = WorldSpeedController.Instance.CurrentSpeed;
            }

            targetKmh = targetSpeedMps * 3.6f;
            targetRpm = CalculateTargetRpm(targetSpeedMps);
        }

        // Smooth speed needle transition
        currentDisplayedKmh = Mathf.Lerp(currentDisplayedKmh, targetKmh, Time.deltaTime * needleSmoothing);
        currentDisplayedRpm = Mathf.Lerp(currentDisplayedRpm, targetRpm, Time.deltaTime * needleSmoothing);

        UpdateSpeedometerNeedle();
        UpdateTachometerNeedle();
    }

    private float CalculateTargetRpm(float speedMps)
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.DeliveryActive)
        {
            return idleRpm;
        }

        if (cachedEngineAudio != null && cachedEngineAudio.isPlaying)
        {
            float pitchNorm = Mathf.InverseLerp(0.8f, 2.2f, cachedEngineAudio.pitch);
            return Mathf.Lerp(idleRpm, maxRpm, pitchNorm);
        }

        float speedNorm = Mathf.Clamp01(speedMps / 28f);
        float gearProgress = (speedNorm * 3f) % 1f;
        float rpm = Mathf.Lerp(idleRpm, maxRpm, Mathf.Pow(gearProgress, 0.85f));
        return Mathf.Clamp(rpm, idleRpm, maxRpm);
    }

    private void UpdateSpeedometerNeedle()
    {
        if (speedometerNeedle == null) return;

        float speedFrac = Mathf.Clamp01(currentDisplayedKmh / maxDisplaySpeedKmh);
        float baseAngle = Mathf.Lerp(speedZeroAngle, speedMaxAngle, speedFrac);

        float jitter = 0f;
        if (currentDisplayedKmh > vibrationThresholdKmh)
        {
            float jitterIntensity = Mathf.InverseLerp(vibrationThresholdKmh, maxDisplaySpeedKmh, currentDisplayedKmh);
            float noise = (Mathf.PerlinNoise(Time.time * jitterFrequency, 0.5f) - 0.5f) * 2f;
            jitter = noise * maxJitterAngle * jitterIntensity;
        }

        float finalAngle = baseAngle + jitter;
        speedometerNeedle.localRotation = speedInitialLocalRot * Quaternion.AngleAxis(finalAngle, speedRotationAxis.normalized);
    }

    private void UpdateTachometerNeedle()
    {
        if (tachometerNeedle == null) return;

        float rpmFrac = Mathf.Clamp01((currentDisplayedRpm - idleRpm) / (maxRpm - idleRpm));
        float baseAngle = Mathf.Lerp(rpmZeroAngle, rpmMaxAngle, rpmFrac);

        float rpmJitter = 0f;
        if (currentDisplayedRpm > idleRpm + 500f)
        {
            float noise = (Mathf.PerlinNoise(0.2f, Time.time * (jitterFrequency * 0.8f)) - 0.5f) * 2f;
            rpmJitter = noise * (maxJitterAngle * 0.6f);
        }

        float finalAngle = baseAngle + rpmJitter;
        tachometerNeedle.localRotation = rpmInitialLocalRot * Quaternion.AngleAxis(finalAngle, rpmRotationAxis.normalized);
    }

    public void SetManualTest(bool enable, float speedKmh = 0f, float rpm = 900f)
    {
        manualTestMode = enable;
        testSpeedKmh = speedKmh;
        testRpm = rpm;
    }
}
