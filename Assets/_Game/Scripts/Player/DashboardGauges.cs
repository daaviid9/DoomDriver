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
    [Tooltip("Transform of the speedometer needle (or dial indicator)")]
    [SerializeField] private Transform speedometerNeedle;

    [Tooltip("Maximum display speed in km/h representing the top of the gauge dial (typically 180 km/h)")]
    [SerializeField] private float maxDisplaySpeedKmh = 180f;

    [Tooltip("Needle angle (in degrees) when speed is 0 km/h")]
    [SerializeField] private float speedZeroAngle = -135f;

    [Tooltip("Needle angle (in degrees) when speed is maxDisplaySpeedKmh")]
    [SerializeField] private float speedMaxAngle = 135f;

    [Tooltip("Local axis around which the speedometer needle rotates")]
    [SerializeField] private Vector3 speedRotationAxis = Vector3.up;

    [Header("Tachometer (RPM Gauge)")]
    [Tooltip("Transform of the tachometer / RPM needle (or dial indicator)")]
    [SerializeField] private Transform tachometerNeedle;

    [Tooltip("Idle RPM value (gauge rest state)")]
    [SerializeField] private float idleRpm = 900f;

    [Tooltip("Maximum redline RPM on the gauge dial (typically 7000 or 8000 RPM)")]
    [SerializeField] private float maxRpm = 7000f;

    [Tooltip("Needle angle (in degrees) when RPM is 0 (or idle)")]
    [SerializeField] private float rpmZeroAngle = -135f;

    [Tooltip("Needle angle (in degrees) when RPM is at max/redline")]
    [SerializeField] private float rpmMaxAngle = 135f;

    [Tooltip("Local axis around which the tachometer needle rotates")]
    [SerializeField] private Vector3 rpmRotationAxis = Vector3.up;

    [Header("Dynamics & Smoothing")]
    [Tooltip("Smoothing speed for needle movement (higher = snappier, lower = smoother lag)")]
    [SerializeField] private float needleSmoothing = 14f;

    [Tooltip("Elastic bounce / overshoot amount when accelerating quickly")]
    [Range(0f, 0.5f)]
    [SerializeField] private float needleElasticity = 0.05f;

    [Header("High-Speed Needle Vibration / Jitter")]
    [Tooltip("Speed in km/h above which the speedometer needle starts vibrating subtely")]
    [SerializeField] private float vibrationThresholdKmh = 60f;

    [Tooltip("Maximum amplitude of jitter oscillation in degrees at max speed")]
    [SerializeField] private float maxJitterAngle = 1.25f;

    [Tooltip("Frequency of needle vibration")]
    [SerializeField] private float jitterFrequency = 32f;

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

    /// <summary>
    /// Auto-locates Clock 2 (Speed) and Clock 1 (RPM) under Clocks hierarchy if not manually wired in Inspector.
    /// Also supports custom spawned / needle child transforms.
    /// </summary>
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
                // Clock 2 is the Speedometer dial (-0.09, 0.02, -0.01) with 'Clock Backgound Speed'
                if (speedometerNeedle == null)
                {
                    var needle = clocksRoot.Find("SpeedNeedle") ?? clocksRoot.Find("Clock 2/Needle") ?? clocksRoot.Find("Clock 2");
                    if (needle != null)
                    {
                        speedometerNeedle = needle;
                        speedInitialLocalRot = needle.localRotation;
                    }
                }

                // Clock 1 is the Tachometer / RPM dial (0.09, 0.02, -0.01) with 'Clock Background Rounds'
                if (tachometerNeedle == null)
                {
                    var needle = clocksRoot.Find("RpmNeedle") ?? clocksRoot.Find("Clock 1/Needle") ?? clocksRoot.Find("Clock 1");
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
        float targetSpeedMps = 0f;

        // Read forward speed from single source of truth (WorldSpeedController)
        if (WorldSpeedController.Instance != null)
        {
            targetSpeedMps = WorldSpeedController.Instance.CurrentSpeed;
        }

        // Convert m/s to km/h (1 m/s = 3.6 km/h)
        float targetKmh = targetSpeedMps * 3.6f;

        // Smooth speed needle transition
        currentDisplayedKmh = Mathf.Lerp(currentDisplayedKmh, targetKmh, Time.deltaTime * needleSmoothing);

        // Compute RPM target based on engine audio pitch or speed ratio
        float targetRpm = CalculateTargetRpm(targetSpeedMps);
        currentDisplayedRpm = Mathf.Lerp(currentDisplayedRpm, targetRpm, Time.deltaTime * needleSmoothing);

        // Apply needle transformations
        UpdateSpeedometerNeedle();
        UpdateTachometerNeedle();
    }

    /// <summary>
    /// Computes engine RPM according to engine audio pitch and speed simulation.
    /// Emulates simulated gear shifts / load for natural instrument gauge movement.
    /// </summary>
    private float CalculateTargetRpm(float speedMps)
    {
        // If parked in lobby or menu, rest at idle
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.DeliveryActive)
        {
            return idleRpm;
        }

        // If Engine AudioSource is available, derive directly from its runtime pitch
        if (cachedEngineAudio != null && cachedEngineAudio.isPlaying)
        {
            float pitchNorm = Mathf.InverseLerp(0.8f, 2.2f, cachedEngineAudio.pitch);
            return Mathf.Lerp(idleRpm, maxRpm, pitchNorm);
        }

        // Fallback: 3-gear simulation based on speed
        float speedNorm = Mathf.Clamp01(speedMps / 28f);
        // Multi-gear tooth profile (3 gears)
        float gearProgress = (speedNorm * 3f) % 1f;
        float rpm = Mathf.Lerp(idleRpm, maxRpm, Mathf.Pow(gearProgress, 0.85f));
        return Mathf.Clamp(rpm, idleRpm, maxRpm);
    }

    private void UpdateSpeedometerNeedle()
    {
        if (speedometerNeedle == null) return;

        float speedFrac = Mathf.Clamp01(currentDisplayedKmh / maxDisplaySpeedKmh);
        float baseAngle = Mathf.Lerp(speedZeroAngle, speedMaxAngle, speedFrac);

        // Add subtle high-speed jitter/vibration
        float jitter = 0f;
        if (currentDisplayedKmh > vibrationThresholdKmh)
        {
            float jitterIntensity = Mathf.InverseLerp(vibrationThresholdKmh, maxDisplaySpeedKmh, currentDisplayedKmh);
            // Perlin noise or high-frequency sine vibration
            float noise = (Mathf.PerlinNoise(Time.time * jitterFrequency, 0.5f) - 0.5f) * 2f;
            jitter = noise * maxJitterAngle * jitterIntensity;
        }

        float finalAngle = baseAngle + jitter;
        speedometerNeedle.localRotation = speedInitialLocalRot * Quaternion.AngleAxis(finalAngle, speedRotationAxis);
    }

    private void UpdateTachometerNeedle()
    {
        if (tachometerNeedle == null) return;

        float rpmFrac = Mathf.Clamp01((currentDisplayedRpm - idleRpm) / (maxRpm - idleRpm));
        float baseAngle = Mathf.Lerp(rpmZeroAngle, rpmMaxAngle, rpmFrac);

        // Slight natural engine pulsation jitter
        float rpmJitter = 0f;
        if (currentDisplayedRpm > idleRpm + 500f)
        {
            float noise = (Mathf.PerlinNoise(0.2f, Time.time * (jitterFrequency * 0.8f)) - 0.5f) * 2f;
            rpmJitter = noise * (maxJitterAngle * 0.6f);
        }

        float finalAngle = baseAngle + rpmJitter;
        tachometerNeedle.localRotation = rpmInitialLocalRot * Quaternion.AngleAxis(finalAngle, rpmRotationAxis);
    }

    /// <summary>
    /// Explicitly configure needle references and parameters.
    /// </summary>
    public void Configure(Transform speedNeedle, Transform rpmNeedle, float maxSpeed = 180f, float maxEngineRpm = 7000f)
    {
        speedometerNeedle = speedNeedle;
        if (speedNeedle != null) speedInitialLocalRot = speedNeedle.localRotation;

        tachometerNeedle = rpmNeedle;
        if (rpmNeedle != null) rpmInitialLocalRot = rpmNeedle.localRotation;

        maxDisplaySpeedKmh = maxSpeed;
        maxRpm = maxEngineRpm;
    }
}
