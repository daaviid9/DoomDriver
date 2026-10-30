using UnityEngine;

/// <summary>
/// Handles player lateral movement across discrete lanes, vehicle body roll (tilt), and steering wheel rotation.
/// Structured with modular methods to allow easy swapping or extension (e.g. to continuous/analog steering).
/// Strictly decoupled from audio, VFX, and health.
/// </summary>
public class VehicleMovement : MonoBehaviour
{
    [Header("Lane Movement Settings")]
    [SerializeField] private float laneDistance = 3f;
    [SerializeField] private float laneChangeSpeed = 10f;
    [SerializeField] private int startingLane = 1; // 0=Left, 1=Middle, 2=Right

    [Header("Steering Wheel Settings")]
    [SerializeField] private Transform steeringWheel;
    [SerializeField] private float maxSteeringAngle = 90f;
    [SerializeField] private float steeringRotationSpeed = 10f;

    [Header("Body Tilt (Roll) Settings")]
    [Tooltip("Maximum roll angle of car body during lateral turn")]
    [SerializeField] private float maxTiltAngle = 2f;
    [Tooltip("Smoothing speed for body roll recovery")]
    [SerializeField] private float tiltSmoothing = 10f;

    private InputSystem_Actions controls;
    private int targetLane = 1;
    private float currentTilt = 0f;
    private float lastX;
    private bool isManeuvering = false;

    public int TargetLane => targetLane;
    public bool IsManeuvering => isManeuvering;

    /// <summary>
    /// Event fired when the target lane changes.
    /// </summary>
    public event System.Action<int> OnLaneChanged;

    /// <summary>
    /// Event fired when vehicle starts or stops lateral lane maneuvering.
    /// </summary>
    public event System.Action<bool> OnManeuveringStateChanged;

    private void Awake()
    {
        controls = new InputSystem_Actions();
        controls.Player.Move.performed += ctx => OnMove(ctx.ReadValue<Vector2>());
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
        ResetMovement();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDeliveryStarted += ResetMovement;
            GameManager.Instance.OnOrderSelectionStarted += ResetMovement;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDeliveryStarted -= ResetMovement;
            GameManager.Instance.OnOrderSelectionStarted -= ResetMovement;
        }
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DeliveryFailed) return;
        if (PauseController.Instance != null && PauseController.Instance.IsPaused) return;

        UpdateMovement();
    }

    /// <summary>
    /// Coordinates lateral position, body tilt, steering wheel, and maneuvering state.
    /// Split into focused private methods for readability and future movement model upgrades.
    /// </summary>
    private void UpdateMovement()
    {
        float targetX = (targetLane - 1) * laneDistance;
        float newX =Mathf.Lerp(transform.position.x, targetX, Time.deltaTime * laneChangeSpeed);
        float movementDelta = newX - lastX;

        transform.position = new Vector3(newX, transform.position.y, transform.position.z);
        ApplyBodyTilt(movementDelta);
        ApplySteeringWheelRotation(targetX);
        UpdateManeuveringState(targetX);

        lastX = newX;
    }

    private void ApplyBodyTilt(float movementDelta)
    {
        float targetTilt = 0f;
        if (Time.deltaTime > 0.0001f)
        {
            targetTilt = -(movementDelta / Time.deltaTime) * (maxTiltAngle / 2f);
        }

        targetTilt = Mathf.Clamp(targetTilt, -maxTiltAngle, maxTiltAngle);
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSmoothing);
        transform.localRotation = Quaternion.Euler(0f, 0f, currentTilt);
    }

    private void ApplySteeringWheelRotation(float targetX)
    {
        if (steeringWheel == null) return;

        float distanceToTarget = targetX - transform.position.x;
        float targetAngle = 0f;

        if (Mathf.Abs(distanceToTarget) > 0.2f)
        {
            targetAngle = (distanceToTarget < 0) ? -maxSteeringAngle : maxSteeringAngle;
        }

        Quaternion targetRot = Quaternion.Euler(0f, 0f, targetAngle);
        steeringWheel.localRotation = Quaternion.Lerp(steeringWheel.localRotation, targetRot, Time.deltaTime * steeringRotationSpeed);
    }

    private void UpdateManeuveringState(float targetX)
    {
        bool moving = Mathf.Abs(targetX - transform.position.x) > 0.05f;
        if (moving != isManeuvering)
        {
            isManeuvering = moving;
            OnManeuveringStateChanged?.Invoke(isManeuvering);
        }
    }

    private void OnMove(Vector2 direction)
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.DeliveryActive) return;
        if (PauseController.Instance != null && PauseController.Instance.IsPaused) return;

        if (direction.x < -0.5f)
        {
            if (targetLane > 0)
            {
                targetLane--;
                OnLaneChanged?.Invoke(targetLane);
            }
        }
        else if (direction.x > 0.5f)
        {
            if (targetLane < 2)
            {
                targetLane++;
                OnLaneChanged?.Invoke(targetLane);
            }
        }
    }

    /// <summary>
    /// Resets the vehicle lateral position, tilt, and steering wheel to center lane.
    /// </summary>
    public void ResetMovement()
    {
        targetLane = startingLane;
        currentTilt = 0f;
        lastX = 0f;
        isManeuvering = false;

        transform.position = new Vector3(0f, transform.position.y, transform.position.z);
        transform.localRotation = Quaternion.identity;

        if (steeringWheel != null)
        {
            steeringWheel.localRotation = Quaternion.identity;
        }

        OnManeuveringStateChanged?.Invoke(false);
    }
}

