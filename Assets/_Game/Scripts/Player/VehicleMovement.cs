using UnityEngine;
using DoomDriver.Player;

/// <summary>
/// Defines available vehicle steering mechanics.
/// Switchable directly in the Unity Inspector.
/// </summary>
public enum SteeringMode
{
    DiscreteLanes,
    AnalogContinuous
}

/// <summary>
/// Handles player lateral movement, curb bounce physics, and scripted turn maneuvers.
/// Delegates input reading to VehicleInputReader, wheel rotation to SteeringWheelVisual,
/// and body roll to VehicleBodyRoll.
/// </summary>
public class VehicleMovement : MonoBehaviour
{
    public static VehicleMovement Instance { get; private set; }

    [Header("Steering Mode")]
    [SerializeField] private SteeringMode steeringMode = SteeringMode.AnalogContinuous;

    [Header("Discrete Lane Settings")]
    [SerializeField] private float laneDistance = 3f;
    [SerializeField] private float laneChangeSpeed = 10f;
    [SerializeField] private int startingLane = 1;

    [Header("Analog Steering Settings")]
    [SerializeField] private float maxSteerSpeed = 8f;
    [SerializeField] private float steerAcceleration = 24f;
    [SerializeField] private float steerDeceleration = 28f;
    [SerializeField] private float minRoadX = -4.5f;
    [SerializeField] private float maxRoadX = 4.5f;

    [Header("Speed-Sensitive Steering")]
    [SerializeField] private float referenceForwardSpeed = 12.1f;
    [SerializeField] private float minSpeedFactor = 0.35f;
    [SerializeField] private float maxSpeedFactor = 1.6f;

    [Header("Curb Interaction Settings")]
    [SerializeField] private float curbBounceForce = 3.5f;
    [SerializeField] private float curbSpeedPenalty = 2.5f;
    [SerializeField] private float curbBounceCooldown = 0.5f;

    [Header("Modular Sub-Components")]
    [SerializeField] private VehicleInputReader inputReader;
    [SerializeField] private SteeringWheelVisual steeringWheelVisual;
    [SerializeField] private VehicleBodyRoll bodyRoll;

    private int targetLane = 1;
    private float lateralVelocity = 0f;
    private float lastX = 0f;
    private bool isManeuvering = false;
    private float lastCurbBounceTime = -1f;
    private Coroutine easeToCenterCoroutine;
    private bool isTurnManeuverActive = false;
    private Coroutine turnManeuverCoroutine;

    public bool IsTurnManeuverActive => isTurnManeuverActive;
    public SteeringMode CurrentMode => steeringMode;
    public int TargetLane => targetLane;
    public float LateralVelocity => lateralVelocity;
    public bool IsManeuvering => isManeuvering;
    public float MinRoadX => minRoadX;
    public float MaxRoadX => maxRoadX;
    public Vector2 MoveInput => inputReader != null ? inputReader.MoveInput : Vector2.zero;

    public event System.Action<int> OnLaneChanged;
    public event System.Action<bool> OnManeuveringStateChanged;
    public event System.Action OnCurbContact;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        ResolveSubComponents();
    }

    private void ResolveSubComponents()
    {
        if (inputReader == null) inputReader = GetComponent<VehicleInputReader>();
        if (steeringWheelVisual == null) steeringWheelVisual = GetComponent<SteeringWheelVisual>();
        if (bodyRoll == null) bodyRoll = GetComponent<VehicleBodyRoll>();
    }

    private void OnEnable()
    {
        if (inputReader != null)
        {
            inputReader.OnMovePerformed -= HandleMovePerformed;
            inputReader.OnMovePerformed += HandleMovePerformed;
        }
    }

    private void OnDisable()
    {
        if (inputReader != null)
        {
            inputReader.OnMovePerformed -= HandleMovePerformed;
        }
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

        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DeliveryFailed) return;
        if (PauseController.Instance != null && PauseController.Instance.IsPaused) return;
        if (isTurnManeuverActive) return;

        if (steeringMode == SteeringMode.DiscreteLanes)
        {
            UpdateDiscreteMovement();
        }
        else
        {
            UpdateAnalogMovement();
        }
    }

    private void HandleMovePerformed(Vector2 direction)
    {
        if (steeringMode != SteeringMode.DiscreteLanes) return;
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.DeliveryActive) return;
        if (PauseController.Instance != null && PauseController.Instance.IsPaused) return;

        if (direction.x < -0.5f && targetLane > 0)
        {
            targetLane--;
            OnLaneChanged?.Invoke(targetLane);
        }
        else if (direction.x > 0.5f && targetLane < 2)
        {
            targetLane++;
            OnLaneChanged?.Invoke(targetLane);
        }
    }

    private void UpdateDiscreteMovement()
    {
        float targetX = (targetLane - 1) * laneDistance;
        float newX = Mathf.Lerp(transform.position.x, targetX, Time.deltaTime * laneChangeSpeed);
        float movementDelta = newX - lastX;

        transform.position = new Vector3(newX, transform.position.y, transform.position.z);
        bodyRoll?.UpdateDiscreteRoll(movementDelta);

        float distanceToTarget = targetX - transform.position.x;
        float wheelTarget = Mathf.Abs(distanceToTarget) > 0.2f ? (distanceToTarget < 0f ? -1f : 1f) : 0f;
        steeringWheelVisual?.UpdateNormalizedTarget(wheelTarget);

        SetManeuveringState(Mathf.Abs(targetX - transform.position.x) > 0.05f);
        lastX = newX;
    }

    private void UpdateAnalogMovement()
    {
        float forwardSpeed = WorldSpeedController.Instance != null ? WorldSpeedController.Instance.CurrentSpeed : 0f;

        if (forwardSpeed <= 0.1f || GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.DeliveryActive)
        {
            lateralVelocity = 0f;
            bodyRoll?.RecoverToNeutral();
            return;
        }

        float inputX = MoveInput.x;
        float speedFactor = Mathf.Clamp(forwardSpeed / referenceForwardSpeed, minSpeedFactor, maxSpeedFactor);
        float effectiveSteerSpeed = maxSteerSpeed * speedFactor;
        float targetVelocity = inputX * effectiveSteerSpeed;

        if (Mathf.Abs(inputX) > 0.05f)
        {
            lateralVelocity = Mathf.MoveTowards(lateralVelocity, targetVelocity, steerAcceleration * speedFactor * Time.deltaTime);
        }
        else
        {
            lateralVelocity = Mathf.MoveTowards(lateralVelocity, 0f, steerDeceleration * Time.deltaTime);
        }

        float newX = transform.position.x + lateralVelocity * Time.deltaTime;

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
        bodyRoll?.UpdateAnalogRoll(lateralVelocity, maxSteerSpeed, speedFactor);
        steeringWheelVisual?.UpdateNormalizedTarget(inputX);
        SetManeuveringState(Mathf.Abs(lateralVelocity) > (effectiveSteerSpeed * 0.35f));

        lastX = newX;
    }

    private void SetManeuveringState(bool moving)
    {
        if (moving == isManeuvering) return;
        isManeuvering = moving;
        OnManeuveringStateChanged?.Invoke(isManeuvering);
    }

    private void TriggerCurbBounce()
    {
        if (isTurnManeuverActive) return;
        if (Time.time - lastCurbBounceTime < curbBounceCooldown) return;
        lastCurbBounceTime = Time.time;

        OnCurbContact?.Invoke();

        if (WorldSpeedController.Instance != null && curbSpeedPenalty > 0f)
        {
            WorldSpeedController.Instance.ApplySpeedPenalty(curbSpeedPenalty);
        }
    }

    public void StartTurnManeuver(DashboardNavigationSource.Maneuver direction, float duration, bool isFork = false)
    {
        if (turnManeuverCoroutine != null)
        {
            StopCoroutine(turnManeuverCoroutine);
        }
        if (easeToCenterCoroutine != null)
        {
            StopCoroutine(easeToCenterCoroutine);
            easeToCenterCoroutine = null;
        }

        turnManeuverCoroutine = StartCoroutine(TurnManeuverRoutine(direction, duration, isFork));
    }

    private System.Collections.IEnumerator TurnManeuverRoutine(DashboardNavigationSource.Maneuver direction, float duration, bool isFork = false)
    {
        isTurnManeuverActive = true;
        isManeuvering = true;

        if (!isFork)
        {
            OnManeuveringStateChanged?.Invoke(true);
        }

        float elapsed = 0f;
        float startX = transform.position.x;

        bool isLeft = direction == DashboardNavigationSource.Maneuver.Left || direction == DashboardNavigationSource.Maneuver.ForkLeft;
        float targetYaw = isFork ? 0f : (isLeft ? -10f : 10f);
        float targetRoll = isFork ? (isLeft ? 0.5f : -0.5f) : (isLeft ? 2.0f : -2.0f);
        float maxWheelAngle = steeringWheelVisual != null ? steeringWheelVisual.MaxSteeringAngle : 90f;
        float targetWheel = isFork
            ? (isLeft ? -maxWheelAngle * 0.35f : maxWheelAngle * 0.35f)
            : (isLeft ? -maxWheelAngle * 0.65f : maxWheelAngle * 0.65f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            float currentX = Mathf.Lerp(startX, 0f, smoothT);
            transform.position = new Vector3(currentX, transform.position.y, transform.position.z);

            float apexWeight = Mathf.Sin(t * Mathf.PI);
            transform.localRotation = Quaternion.Euler(0f, targetYaw * apexWeight, targetRoll * apexWeight);
            steeringWheelVisual?.SetDirectAngle(targetWheel * apexWeight);

            yield return null;
        }

        EndTurnManeuver();
    }

    public void EndTurnManeuver()
    {
        if (turnManeuverCoroutine != null)
        {
            StopCoroutine(turnManeuverCoroutine);
            turnManeuverCoroutine = null;
        }

        isTurnManeuverActive = false;
        isManeuvering = false;
        lateralVelocity = 0f;
        transform.position = new Vector3(0f, transform.position.y, transform.position.z);
        bodyRoll?.ResetRoll();
        transform.localRotation = Quaternion.identity;
        steeringWheelVisual?.ResetRotation();

        OnManeuveringStateChanged?.Invoke(false);
    }

    public void EaseToCenter(float duration = 1.0f)
    {
        if (easeToCenterCoroutine != null)
        {
            StopCoroutine(easeToCenterCoroutine);
        }
        easeToCenterCoroutine = StartCoroutine(EaseToCenterRoutine(duration));
    }

    private System.Collections.IEnumerator EaseToCenterRoutine(float duration)
    {
        float elapsed = 0f;
        float startX = transform.position.x;

        while (elapsed < duration)
        {
            if (Mathf.Abs(MoveInput.x) > 0.1f)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            float currentX = Mathf.Lerp(startX, 0f, t);
            transform.position = new Vector3(currentX, transform.position.y, transform.position.z);
            yield return null;
        }

        easeToCenterCoroutine = null;
    }

    public void ResetMovement()
    {
        EndTurnManeuver();

        if (easeToCenterCoroutine != null)
        {
            StopCoroutine(easeToCenterCoroutine);
            easeToCenterCoroutine = null;
        }

        targetLane = startingLane;
        inputReader?.ResetInput();
        lateralVelocity = 0f;
        lastX = 0f;
        isManeuvering = false;

        transform.position = new Vector3(0f, transform.position.y, transform.position.z);
        bodyRoll?.ResetRoll();
        transform.localRotation = Quaternion.identity;
        steeringWheelVisual?.ResetRotation();

        OnManeuveringStateChanged?.Invoke(false);
    }

    private void OnDrawGizmosSelected()
    {
        float y = transform.position.y;
        float z = transform.position.z;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(minRoadX, y, z - 20f), new Vector3(minRoadX, y, z + 60f));
        Gizmos.DrawLine(new Vector3(maxRoadX, y, z - 20f), new Vector3(maxRoadX, y, z + 60f));

        for (float offsetZ = -20f; offsetZ <= 60f; offsetZ += 10f)
        {
            Gizmos.DrawLine(new Vector3(minRoadX, y, z + offsetZ), new Vector3(minRoadX, y + 0.6f, z + offsetZ));
            Gizmos.DrawLine(new Vector3(maxRoadX, y, z + offsetZ), new Vector3(maxRoadX, y + 0.6f, z + offsetZ));
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector3(0f, y, z - 20f), new Vector3(0f, y, z + 60f));

        if (steeringMode == SteeringMode.DiscreteLanes)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
            Gizmos.DrawLine(new Vector3(-laneDistance, y, z - 20f), new Vector3(-laneDistance, y, z + 60f));
            Gizmos.DrawLine(new Vector3(laneDistance, y, z - 20f), new Vector3(laneDistance, y, z + 60f));
        }
    }
}
