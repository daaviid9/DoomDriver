using UnityEngine;

/// <summary>
/// Controls the speed and acceleration of the moving world during driving.
/// Serves as the Single Source of Truth for forward speed metrics in the project.
/// Decouples vehicle movement dynamics and road scrolling from the high-level game state machine.
/// </summary>
public class WorldSpeedController : MonoBehaviour
{
    public static WorldSpeedController Instance { get; private set; }

    [Header("Speed State")]
    [Tooltip("Current speed of the moving world in meters per second")]
    [SerializeField] private float currentSpeed = 0f;

    [Header("Speed Settings")]
    [Tooltip("Starting cruise speed when a delivery begins (auto-driving minimum)")]
    [SerializeField] private float defaultCruiseSpeed = 12f;

    [Tooltip("Maximum top speed achievable when player accelerates")]
    [SerializeField] private float topSpeed = 28f;

    [Tooltip("Acceleration rate when player holds gas (W / Up / Stick Up)")]
    [SerializeField] private float playerAccelerationRate = 10f;

    [Tooltip("Deceleration rate when player releases gas, returning to base cruise speed")]
    [SerializeField] private float naturalDecelerationRate = 6f;

    [Tooltip("Player active braking deceleration rate (S / Down / Stick Down)")]
    [SerializeField] private float playerBrakeDeceleration = 14f;

    [Tooltip("Minimum speed allowed when braking or after collision slowdown")]
    [SerializeField] private float minSpeed = 5f;

    [Header("Braking / Traffic Stop")]
    [Tooltip("Deceleration rate applied when hard braking (e.g., at red lights)")]
    [SerializeField] private float brakeDeceleration = 20f;
    private bool isBraking = false;

    private InputSystem_Actions controls;

    public float CurrentSpeed => currentSpeed;
    public float DefaultCruiseSpeed => defaultCruiseSpeed;
    public float TopSpeed => topSpeed;
    public float MinSpeed => minSpeed;

    public event System.Action<float> OnSpeedChanged;

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

        controls = new InputSystem_Actions();
        currentSpeed = 0f;
    }

    private void OnEnable()
    {
        controls?.Enable();
    }

    private void OnDisable()
    {
        controls?.Disable();
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            HandleGameStateChanged(GameManager.Instance.CurrentState);
        }
        else
        {
            StopWorld();
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.DeliveryActive) return;
        if (PauseController.Instance != null && PauseController.Instance.IsPaused) return;

        if (isBraking)
        {
            // Decelerate down to full stop (traffic red light or emergency)
            if (currentSpeed > 0f)
            {
                currentSpeed = Mathf.Max(0f, currentSpeed - brakeDeceleration * Time.deltaTime);
                OnSpeedChanged?.Invoke(currentSpeed);
            }
            return;
        }

        float forwardInput = 0f;
        if (controls != null)
        {
            forwardInput = controls.Player.Move.ReadValue<Vector2>().y;
        }

        float previousSpeed = currentSpeed;

        if (forwardInput > 0.05f)
        {
            // Player is accelerating up towards topSpeed
            if (currentSpeed < topSpeed)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, topSpeed, playerAccelerationRate * forwardInput * Time.deltaTime);
            }
        }
        else if (forwardInput < -0.05f)
        {
            // Player is braking down towards minSpeed
            if (currentSpeed > minSpeed)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, minSpeed, playerBrakeDeceleration * (-forwardInput) * Time.deltaTime);
            }
        }
        else
        {
            // No forward/backward input: auto-cruise behavior converging to defaultCruiseSpeed
            if (!Mathf.Approximately(currentSpeed, defaultCruiseSpeed))
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, defaultCruiseSpeed, naturalDecelerationRate * Time.deltaTime);
            }
        }

        if (!Mathf.Approximately(previousSpeed, currentSpeed))
        {
            OnSpeedChanged?.Invoke(currentSpeed);
        }
    }

    /// <summary>
    /// Sets whether the vehicle is actively braking (e.g., at a red light).
    /// </summary>
    public void SetBraking(bool braking)
    {
        isBraking = braking;
    }

    /// <summary>
    /// Applies a sudden speed penalty caused by an obstacle or curb collision.
    /// </summary>
    public void ApplySpeedPenalty(float amount)
    {
        currentSpeed = Mathf.Max(minSpeed, currentSpeed - amount);
        OnSpeedChanged?.Invoke(currentSpeed);
        Debug.Log($"<color=orange>[SPEED PENALTY]</color> Slowed by {amount}. Current speed: {currentSpeed:F1}");
    }

    /// <summary>
    /// Instantly halts world movement (used in lobby, game over, or delivery completion).
    /// </summary>
    private void StopWorld()
    {
        currentSpeed = 0f;
        OnSpeedChanged?.Invoke(0f);
    }

    /// <summary>
    /// Resets world speed to starting cruise speed for active delivery.
    /// </summary>
    private void ResetToCruiseSpeed()
    {
        currentSpeed = defaultCruiseSpeed;
        OnSpeedChanged?.Invoke(defaultCruiseSpeed);
    }

    private void HandleGameStateChanged(GameState newState)
    {
        switch (newState)
        {
            case GameState.OrderSelection:
            case GameState.DeliveryCompleted:
            case GameState.DeliveryFailed:
                StopWorld();
                break;

            case GameState.DeliveryActive:
                ResetToCruiseSpeed();
                break;
        }
    }
}
