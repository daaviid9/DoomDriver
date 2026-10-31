using UnityEngine;

/// <summary>
/// Defines available vehicle steering mechanics.
/// Switchable directly in the Unity Inspector.
/// </summary>
public enum SteeringMode
{
    DiscreteLanes,     // Classic 3-lane stepping (Left, Center, Right)
    AnalogContinuous   // Smooth analog steering with momentum, speed sensitivity, and curb bounce boundaries
}

/// <summary>
/// Handles player lateral movement, vehicle body roll (tilt), and steering wheel rotation.
/// Supports both discrete lane shifting and continuous analog steering (switchable in Inspector).
/// Incorporates speed-sensitive steering kinematics based on forward world movement speed.
/// Strictly decoupled from audio, VFX, and health.
/// </summary>
public class VehicleMovement : MonoBehaviour
{
    [Header("Steering Mode")]
    [Tooltip("Switch between discrete 3-lane steps or smooth analog steering")]
    [SerializeField] private SteeringMode steeringMode = SteeringMode.AnalogContinuous;

    [Header("Discrete Lane Settings")]
    [SerializeField] private float laneDistance = 3f;
    [SerializeField] private float laneChangeSpeed = 10f;
    [SerializeField] private int startingLane = 1; // 0=Left, 1=Middle, 2=Right

    [Header("Analog Steering Settings")]
    [Tooltip("Maximum lateral steering speed in meters per second at base forward speed")]
    [SerializeField] private float maxSteerSpeed = 8f;

    [Tooltip("Lateral acceleration rate when steering input is applied")]
    [SerializeField] private float steerAcceleration = 24f;

    [Tooltip("Lateral deceleration rate when steering input returns to neutral")]
    [SerializeField] private float steerDeceleration = 28f;

    [Tooltip("Left road curb boundary (minimum X)")]
    [SerializeField] private float minRoadX = -4.5f;

    [Tooltip("Right road curb boundary (maximum X)")]
    [SerializeField] private float maxRoadX = 4.5f;

    [Header("Speed-Sensitive Steering")]
    [Tooltip("Reference forward speed (m/s) where steering responsiveness is 1.0x (normal)")]
    [SerializeField] private float referenceForwardSpeed = 12f;

    [Tooltip("Minimum steering factor so vehicle remains controllable at low speeds")]
    [SerializeField] private float minSpeedFactor = 0.35f;

    [Tooltip("Maximum steering factor scaling at high forward speeds")]
    [SerializeField] private float maxSpeedFactor = 1.6f;

    [Header("Curb Interaction Settings")]
    [Tooltip("Impulse speed bouncing the car back inward upon curb contact")]
    [SerializeField] private float curbBounceForce = 3.5f;

    [Tooltip("Speed penalty applied to the world when contacting the curb")]
    [SerializeField] private float curbSpeedPenalty = 2.5f;

    [Header("Steering Wheel Settings")]
    [SerializeField] private Transform steeringWheel;
    [SerializeField] private float maxSteeringAngle = 90f;
    [SerializeField] private float steeringRotationSpeed = 12f;

    [Header("Body Tilt (Roll) Settings")]
    [Tooltip("Maximum roll angle of car body during lateral turn")]
    [SerializeField] private float maxTiltAngle = 2.5f;

    [Tooltip("Smoothing speed for body roll recovery")]
    [SerializeField] private float tiltSmoothing = 10f;

    private InputSystem_Actions controls;
    private Vector2 moveInput = Vector2.zero;
    private int targetLane = 1;
    private float lateralVelocity = 0f;
    private float currentTilt = 0f;
    private float lastX = 0f;
    private bool isManeuvering = false;

    public SteeringMode CurrentMode => steeringMode;
    public int TargetLane => targetLane;
    public float LateralVelocity => lateralVelocity;
    public bool IsManeuvering => isManeuvering;
    public float MinRoadX => minRoadX;
    public float MaxRoadX => maxRoadX;
    public Vector2 MoveInput => moveInput;

    /// <summary>
    /// Event fired when target lane changes in discrete mode.
    /// </summary>
    public event System.Action<int> OnLaneChanged;

    /// <summary>
    /// Event fired when vehicle starts or stops lateral lane maneuvering.
    /// </summary>
    public event System.Action<bool> OnManeuveringStateChanged;

    /// <summary>
    /// Event fired when vehicle contacts or bounces off the road curb/edge.
    /// </summary>
    public event System.Action OnCurbContact;

    private void Awake()
    {
        controls = new InputSystem_Actions();

        // Event-driven input handling: capture input updates and neutral releases
        controls.Player.Move.performed += ctx =>
        {
            moveInput = ctx.ReadValue<Vector2>();

            if (steeringMode == SteeringMode.DiscreteLanes)
            {
                OnDiscreteMove(moveInput);
            }
        };

        controls.Player.Move.canceled += ctx =>
        {
            moveInput = Vector2.zero;
        };
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
    /// Dispatches movement execution to the selected steering model.
    /// </summary>
    private void UpdateMovement()
    {
        if (steeringMode == SteeringMode.DiscreteLanes)
        {
            UpdateDiscreteMovement();
        }
        else
        {
            UpdateAnalogMovement();
        }
    }

    #region Discrete Lane Steering

    private void UpdateDiscreteMovement()
    {
        float targetX = (targetLane - 1) * laneDistance;
        float newX = Mathf.Lerp(transform.position.x, targetX, Time.deltaTime * laneChangeSpeed);
        float movementDelta = newX - lastX;

        transform.position = new Vector3(newX, transform.position.y, transform.position.z);
        ApplyDiscreteBodyTilt(movementDelta);
        ApplyDiscreteSteeringWheel(targetX);
        UpdateDiscreteManeuveringState(targetX);

        lastX = newX;
    }

    private void ApplyDiscreteBodyTilt(float movementDelta)
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

    private void ApplyDiscreteSteeringWheel(float targetX)
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

    private void UpdateDiscreteManeuveringState(float targetX)
    {
        bool moving = Mathf.Abs(targetX - transform.position.x) > 0.05f;
        if (moving != isManeuvering)
        {
            isManeuvering = moving;
            OnManeuveringStateChanged?.Invoke(isManeuvering);
        }
    }

    private void OnDiscreteMove(Vector2 direction)
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

    #endregion

    #region Analog Continuous Steering (Speed-Sensitive)

    private void UpdateAnalogMovement()
    {
        float forwardSpeed = WorldSpeedController.Instance != null ? WorldSpeedController.Instance.CurrentSpeed : 0f;

        // If the vehicle is stationary (e.g. parked in lobby or emergency stopped), lateral motion is zero
        if (forwardSpeed <= 0.1f || GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.DeliveryActive)
        {
            lateralVelocity = 0f;
            currentTilt = Mathf.Lerp(currentTilt, 0f, Time.deltaTime * tiltSmoothing);
            transform.localRotation = Quaternion.Euler(0f, 0f, currentTilt);
            return;
        }

        // Calculate dynamic speed factor: scales steering response proportionally to forward momentum
        float speedFactor = Mathf.Clamp(forwardSpeed / referenceForwardSpeed, minSpeedFactor, maxSpeedFactor);

        // Effective lateral steering speed scaled by forward speed
        float effectiveSteerSpeed = maxSteerSpeed * speedFactor;
        float targetVelocity = moveInput.x * effectiveSteerSpeed;

        // Accelerate or decelerate lateral velocity based on cached moveInput
        if (Mathf.Abs(moveInput.x) > 0.05f)
        {
            lateralVelocity = Mathf.MoveTowards(lateralVelocity, targetVelocity, steerAcceleration * speedFactor * Time.deltaTime);
        }
        else
        {
            lateralVelocity = Mathf.MoveTowards(lateralVelocity, 0f, steerDeceleration * Time.deltaTime);
        }

        // Calculate prospective X position
        float newX = transform.position.x + lateralVelocity * Time.deltaTime;

        // Soft curb bounce interaction at road boundaries
        if (newX <= minRoadX)
        {
            newX = minRoadX;
            if (lateralVelocity < 0f)
            {
                lateralVelocity = curbBounceForce;
                TriggerCurbBounce();
            }
        }
        else if (newX >= maxRoadX)
        {
            newX = maxRoadX;
            if (lateralVelocity > 0f)
            {
                lateralVelocity = -curbBounceForce;
                TriggerCurbBounce();
            }
        }

        transform.position = new Vector3(newX, transform.position.y, transform.position.z);
        ApplyAnalogBodyTilt(speedFactor);
        ApplyAnalogSteeringWheel(moveInput.x);
        UpdateAnalogManeuveringState(effectiveSteerSpeed);

        lastX = newX;
    }

    private void ApplyAnalogBodyTilt(float speedFactor)
    {
        // Body roll tilts opposite to the lateral turn direction, amplified by speed factor
        float speedRatio = maxSteerSpeed > 0.01f ? (lateralVelocity / maxSteerSpeed) : 0f;
        float targetTilt = -speedRatio * maxTiltAngle * speedFactor;
        targetTilt = Mathf.Clamp(targetTilt, -maxTiltAngle * 1.5f, maxTiltAngle * 1.5f);

        currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSmoothing);
        transform.localRotation = Quaternion.Euler(0f, 0f, currentTilt);
    }

    private void ApplyAnalogSteeringWheel(float inputX)
    {
        if (steeringWheel == null) return;

        // Rotate wheel proportionally to steering input intensity (-1 to 1) -> (-maxSteeringAngle to +maxSteeringAngle)
        float targetAngle = inputX * maxSteeringAngle;
        Quaternion targetRot = Quaternion.Euler(0f, 0f, targetAngle);
        steeringWheel.localRotation = Quaternion.Lerp(steeringWheel.localRotation, targetRot, Time.deltaTime * steeringRotationSpeed);
    }

    private void UpdateAnalogManeuveringState(float effectiveSteerSpeed)
    {
        // Sharp turn threshold triggers tire screech audio
        bool moving = Mathf.Abs(lateralVelocity) > (effectiveSteerSpeed * 0.35f);
        if (moving != isManeuvering)
        {
            isManeuvering = moving;
            OnManeuveringStateChanged?.Invoke(isManeuvering);
        }
    }

    private void TriggerCurbBounce()
    {
        OnCurbContact?.Invoke();

        if (WorldSpeedController.Instance != null && curbSpeedPenalty > 0f)
        {
            WorldSpeedController.Instance.ApplySpeedPenalty(curbSpeedPenalty);
        }
    }

    #endregion

    /// <summary>
    /// Resets the vehicle lateral position, velocity, tilt, and steering wheel to neutral center.
    /// </summary>
    public void ResetMovement()
    {
        targetLane = startingLane;
        moveInput = Vector2.zero;
        lateralVelocity = 0f;
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

    /// <summary>
    /// Visualizes road boundaries, lane positions, and center line in the Unity Scene View.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        float y = transform.position.y;
        float z = transform.position.z;

        // Draw Left & Right Road Curbs (Yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(minRoadX, y, z - 20f), new Vector3(minRoadX, y, z + 60f));
        Gizmos.DrawLine(new Vector3(maxRoadX, y, z - 20f), new Vector3(maxRoadX, y, z + 60f));

        // Draw vertical curb tick markers along boundaries
        for (float offsetZ = -20f; offsetZ <= 60f; offsetZ += 10f)
        {
            Gizmos.DrawLine(new Vector3(minRoadX, y, z + offsetZ), new Vector3(minRoadX, y + 0.6f, z + offsetZ));
            Gizmos.DrawLine(new Vector3(maxRoadX, y, z + offsetZ), new Vector3(maxRoadX, y + 0.6f, z + offsetZ));
        }

        // Draw Road Center Line (Cyan)
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector3(0f, y, z - 20f), new Vector3(0f, y, z + 60f));

        // If discrete mode is selected, also draw discrete lane guide markers
        if (steeringMode == SteeringMode.DiscreteLanes)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
            Gizmos.DrawLine(new Vector3(-laneDistance, y, z - 20f), new Vector3(-laneDistance, y, z + 60f));
            Gizmos.DrawLine(new Vector3(laneDistance, y, z - 20f), new Vector3(laneDistance, y, z + 60f));
        }
    }
}
