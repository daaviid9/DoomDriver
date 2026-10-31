using UnityEngine;

/// <summary>
/// Tracks delivery mission metrics (distance traveled, time elapsed, highscore).
/// Follows Single Responsibility Principle (SRP) by keeping metric accumulation out of GameManager.
/// </summary>
public class DeliveryProgressTracker : MonoBehaviour
{
    public static DeliveryProgressTracker Instance { get; private set; }

    [Header("Mission Metrics")]
    [SerializeField] private float currentDistance = 0f;
    [SerializeField] private float deliveryTimeElapsed = 0f;
    [SerializeField] private float highscore = 0f;

    public float CurrentDistance => currentDistance;
    public float DeliveryTimeElapsed => deliveryTimeElapsed;
    public float Highscore => highscore;

    /// <summary>
    /// Event fired whenever the accumulated distance changes.
    /// </summary>
    public event System.Action<float> OnDistanceUpdated;

    /// <summary>
    /// Event fired whenever the mission timer updates.
    /// </summary>
    public event System.Action<float> OnTimeUpdated;

    private const string HighscoreKey = "Highscore";

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

        highscore = PlayerPrefs.GetFloat(HighscoreKey, 0f);
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDeliveryStarted += ResetProgress;
            GameManager.Instance.OnDeliveryFailed += CheckHighscore;
            GameManager.Instance.OnDeliveryCompleted += CheckHighscore;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDeliveryStarted -= ResetProgress;
            GameManager.Instance.OnDeliveryFailed -= CheckHighscore;
            GameManager.Instance.OnDeliveryCompleted -= CheckHighscore;
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.DeliveryActive) return;
        if (PauseController.Instance != null && PauseController.Instance.IsPaused) return;

        deliveryTimeElapsed += Time.deltaTime;
        float forwardSpeed = WorldSpeedController.Instance != null ? WorldSpeedController.Instance.CurrentSpeed : 0f;
        currentDistance += forwardSpeed * Time.deltaTime;

        OnTimeUpdated?.Invoke(deliveryTimeElapsed);
        OnDistanceUpdated?.Invoke(currentDistance);
    }

    /// <summary>
    /// Resets progress counters when a new delivery begins.
    /// </summary>
    private void ResetProgress()
    {
        currentDistance = 0f;
        deliveryTimeElapsed = 0f;
        OnDistanceUpdated?.Invoke(currentDistance);
        OnTimeUpdated?.Invoke(deliveryTimeElapsed);
    }

    /// <summary>
    /// Checks and saves highscore if the current run surpassed previous records.
    /// </summary>
    private void CheckHighscore()
    {
        if (currentDistance > highscore)
        {
            highscore = currentDistance;
            PlayerPrefs.SetFloat(HighscoreKey, highscore);
            PlayerPrefs.Save();
        }
    }
}
