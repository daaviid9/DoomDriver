using UnityEngine;

/// <summary>
/// Manages vehicle engine pitch, crash sound effects, tire screech, and smoke hissing.
/// Listens to events from VehicleHealth and VehicleMovement.
/// Strictly decoupled from movement, collision physics, and particle systems.
/// </summary>
public class VehicleAudio : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource engineSource;
    [SerializeField] private AudioSource hissingSource;
    [SerializeField] private AudioSource screechSource;

    [Header("Crash Audio Clips")]
    [SerializeField] private AudioClip crashLightClip;
    [SerializeField] private AudioClip crashHeavyClip;

    [Header("Engine Pitch Settings")]
    [SerializeField] private float minPitch = 0.8f;
    [SerializeField] private float maxPitch = 2.2f;

    private VehicleMovement movement;
    private VehicleHealth health;

    private void Awake()
    {
        if (movement == null) movement = GetComponent<VehicleMovement>();
        if (health == null) health = GetComponent<VehicleHealth>();
    }

    private void Start()
    {
        ResetAudio();

        if (movement != null)
        {
            movement.OnManeuveringStateChanged += HandleManeuveringStateChanged;
        }

        if (health != null)
        {
            health.OnDamageTaken += HandleDamageTaken;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDeliveryStarted += ResetAudio;
            GameManager.Instance.OnOrderSelectionStarted += ResetAudio;
        }
    }

    private void OnDestroy()
    {
        if (movement != null)
        {
            movement.OnManeuveringStateChanged -= HandleManeuveringStateChanged;
        }

        if (health != null)
        {
            health.OnDamageTaken -= HandleDamageTaken;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDeliveryStarted -= ResetAudio;
            GameManager.Instance.OnOrderSelectionStarted -= ResetAudio;
        }
    }

    private void Update()
    {
        UpdateEngineAudio();
    }

    private void UpdateEngineAudio()
    {
        if (engineSource == null) return;

        float speedRatio = Mathf.Clamp01(WorldMover.moveSpeed / 25f);
        engineSource.pitch = Mathf.Lerp(minPitch, maxPitch, speedRatio);
        engineSource.volume = Mathf.Lerp(0.6f, 1.0f, speedRatio);
    }

    private void HandleManeuveringStateChanged(bool isManeuvering)
    {
        if (screechSource == null) return;

        bool isDriving = GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DeliveryActive;
        if (isManeuvering && isDriving)
        {
            if (!screechSource.isPlaying) screechSource.Play();
        }
        else
        {
            if (screechSource.isPlaying) screechSource.Stop();
        }
    }

    private void HandleDamageTaken(int dmg)
    {
        if (engineSource == null) return;

        if (dmg >= 50 && crashHeavyClip != null)
        {
            engineSource.PlayOneShot(crashHeavyClip);
        }
        else if (crashLightClip != null)
        {
            engineSource.PlayOneShot(crashLightClip);
        }
    }

    public void SetHissing(bool hissing)
    {
        if (hissingSource == null) return;

        if (hissing)
        {
            if (!hissingSource.isPlaying)
            {
                hissingSource.loop = true;
                hissingSource.Play();
            }
        }
        else
        {
            if (hissingSource.isPlaying)
            {
                hissingSource.Stop();
            }
        }
    }

    public void ResetAudio()
    {
        if (hissingSource != null && hissingSource.isPlaying) hissingSource.Stop();
        if (screechSource != null && screechSource.isPlaying) screechSource.Stop();

        if (engineSource != null && !engineSource.isPlaying)
        {
            engineSource.Play();
        }
    }
}
