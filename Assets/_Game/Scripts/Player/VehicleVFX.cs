using UnityEngine;
using System.Collections;

/// <summary>
/// Manages smoke particles, explosion effects, and bump animations for the vehicle.
/// Decoupled from damage logic and audio playback. Listens to events from VehicleHealth and VehicleCollisionHandler.
/// </summary>
public class VehicleVFX : MonoBehaviour
{
    [Header("Particle Effects")]
    [SerializeField] private GameObject smokeLightEffect;
    [SerializeField] private GameObject smokeHeavyEffect;
    [SerializeField] private GameObject explosionEffect;

    [Header("Bump Jump Animation")]
    [SerializeField] private float jumpHeight = 0.5f;
    [SerializeField] private float jumpDuration = 0.3f;

    private VehicleHealth health;
    private VehicleAudio vehicleAudio;
    private VehicleCollisionHandler collisionHandler;

    private bool isJumping = false;

    private void Awake()
    {
        if (health == null) health = GetComponent<VehicleHealth>();
        if (vehicleAudio == null) vehicleAudio = GetComponent<VehicleAudio>();
        if (collisionHandler == null) collisionHandler = GetComponent<VehicleCollisionHandler>();

        ForceStopAndHide(smokeLightEffect);
        ForceStopAndHide(smokeHeavyEffect);
        ForceStopAndHide(explosionEffect);
    }

    private void Start()
    {
        ResetVFX();

        if (health != null)
        {
            health.OnHealthChanged += HandleHealthChanged;
            health.OnDamageTaken += HandleDamageTaken;
        }

        if (collisionHandler != null)
        {
            collisionHandler.OnObstacleBump += HandleObstacleBump;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDeliveryStarted += ResetVFX;
            GameManager.Instance.OnOrderSelectionStarted += ResetVFX;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnHealthChanged -= HandleHealthChanged;
            health.OnDamageTaken -= HandleDamageTaken;
        }

        if (collisionHandler != null)
        {
            collisionHandler.OnObstacleBump -= HandleObstacleBump;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDeliveryStarted -= ResetVFX;
            GameManager.Instance.OnOrderSelectionStarted -= ResetVFX;
        }
    }

    private void HandleDamageTaken(int dmg)
    {
        // Major collision explosion effect
        if (dmg >= 50)
        {
            PlayParticle(explosionEffect);
        }
    }

    private void HandleObstacleBump()
    {
        TriggerBumpJump();
    }

    private void HandleHealthChanged(int currentHP, int maxHP)
    {
        if (currentHP <= 0)
        {
            PlayParticle(explosionEffect);
            if (smokeLightEffect != null) smokeLightEffect.SetActive(false);
            if (smokeHeavyEffect != null) smokeHeavyEffect.SetActive(false);
            if (vehicleAudio != null) vehicleAudio.SetHissing(false);
            return;
        }

        if (currentHP <= 30)
        {
            if (smokeLightEffect != null) smokeLightEffect.SetActive(false);
            if (smokeHeavyEffect != null && !smokeHeavyEffect.activeSelf) PlayParticle(smokeHeavyEffect);
            if (vehicleAudio != null) vehicleAudio.SetHissing(true);
            return;
        }

        if (currentHP <= 50)
        {
            if (smokeLightEffect != null && !smokeLightEffect.activeSelf) PlayParticle(smokeLightEffect);
            if (smokeHeavyEffect != null) smokeHeavyEffect.SetActive(false);
            if (vehicleAudio != null) vehicleAudio.SetHissing(true);
            return;
        }

        // Good health (> 50)
        if (smokeLightEffect != null) smokeLightEffect.SetActive(false);
        if (smokeHeavyEffect != null) smokeHeavyEffect.SetActive(false);
        if (vehicleAudio != null) vehicleAudio.SetHissing(false);
    }

    public void TriggerBumpJump()
    {
        if (!isJumping && gameObject.activeInHierarchy)
        {
            StartCoroutine(JumpRoutine());
        }
    }

    public void ResetVFX()
    {
        ForceStopAndHide(smokeLightEffect);
        ForceStopAndHide(smokeHeavyEffect);
        ForceStopAndHide(explosionEffect);
        isJumping = false;
    }

    private void PlayParticle(GameObject obj)
    {
        if (obj == null) return;
        obj.SetActive(true);

        ParticleSystem ps = obj.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Play(true);
        }
        else
        {
            ParticleSystem childPs = obj.GetComponentInChildren<ParticleSystem>();
            if (childPs != null) childPs.Play(true);
        }
    }

    private void ForceStopAndHide(GameObject obj)
    {
        if (obj == null) return;

        ParticleSystem[] allParticles = obj.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in allParticles)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear();
            var main = ps.main;
            main.playOnAwake = false;
        }

        obj.SetActive(false);
    }

    private IEnumerator JumpRoutine()
    {
        isJumping = true;
        Vector3 startPos = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / jumpDuration;
            float yOffset = Mathf.Sin(normalizedTime * Mathf.PI) * jumpHeight;
            transform.localPosition = new Vector3(startPos.x, startPos.y + yOffset, startPos.z);
            yield return null;
        }

        transform.localPosition = startPos;
        isJumping = false;
    }
}
