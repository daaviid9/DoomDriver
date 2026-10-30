using UnityEngine;

/// <summary>
/// Controls the speed and acceleration of the moving world during driving.
/// Decouples vehicle movement dynamics and road scrolling from the high-level game state machine.
/// </summary>
public class WorldSpeedController : MonoBehaviour
{
    public static WorldSpeedController Instance { get; private set; }

    [Header("Speed Settings")]
    [Tooltip("Starting cruise speed when a delivery begins")]
    [SerializeField] private float defaultCruiseSpeed = 12f;

    [Tooltip("How much world speed increases each second during active delivery")]
    [SerializeField] private float speedIncreaseRate = 0.5f;

    [Tooltip("Maximum allowed world speed (limit)")]
    [SerializeField] private float maxSpeed = 100f;

    [Tooltip("Minimum speed allowed when braking or after collision slowdown")]
    [SerializeField] private float minSpeed = 5f;

    [Header("Braking / Traffic Stop")]
    [Tooltip("Deceleration rate applied when braking (e.g., at red lights)")]
    [SerializeField] private float brakeDeceleration = 20f;
    private bool isBraking = false;

    public float CurrentSpeed => WorldMover.moveSpeed;
    public float DefaultCruiseSpeed => defaultCruiseSpeed;
    public float SpeedIncreaseRate => speedIncreaseRate;
    public float MaxSpeed => maxSpeed;
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

        // Guarantee that world speed starts at 0 while parked in the lobby
        WorldMover.moveSpeed = 0f;
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            // Align speed immediately with current state
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
            // Decelerate down to full stop
            if (WorldMover.moveSpeed > 0f)
            {
                WorldMover.moveSpeed = Mathf.Max(0f, WorldMover.moveSpeed - brakeDeceleration * Time.deltaTime);
                OnSpeedChanged?.Invoke(WorldMover.moveSpeed);
            }
        }
        else
        {
            // Gradually accelerate over time up to maximum cruise speed
            if (WorldMover.moveSpeed < maxSpeed)
            {
                WorldMover.moveSpeed += speedIncreaseRate * Time.deltaTime;
                if (WorldMover.moveSpeed > maxSpeed)
                {
                    WorldMover.moveSpeed = maxSpeed;
                }
                OnSpeedChanged?.Invoke(WorldMover.moveSpeed);
            }
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
    /// Applies a sudden speed penalty caused by an obstacle collision.
    /// </summary>
    public void ApplySpeedPenalty(float amount)
    {
        WorldMover.moveSpeed = Mathf.Max(minSpeed, WorldMover.moveSpeed - amount);
        OnSpeedChanged?.Invoke(WorldMover.moveSpeed);
        Debug.Log($"<color=orange>[SPEED PENALTY]</color> Slowed by {amount}. Current speed: {WorldMover.moveSpeed:F1}");
    }

    /// <summary>
    /// Instantly halts world movement (used in lobby, game over, or delivery completion).
    /// </summary>
    private void StopWorld()
    {
        WorldMover.moveSpeed = 0f;
        OnSpeedChanged?.Invoke(0f);
    }

    /// <summary>
    /// Resets world speed to starting cruise speed for active delivery.
    /// </summary>
    private void ResetToCruiseSpeed()
    {
        WorldMover.moveSpeed = defaultCruiseSpeed;
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
