using UnityEngine;
using System.Collections;

/// <summary>
/// Manages all visual and camera feedback for the player vehicle:
/// - Smoke and explosion particle systems
/// - Obstacle hop/bump jump animation
/// - Impact camera shakes (curb contact and obstacle collisions)
/// Strictly decoupled from movement physics and damage calculation.
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

    [Header("Camera Shake Settings")]
    [Tooltip("Camera shake intensity for minor obstacle collisions (< 50 damage)")]
    [SerializeField] private float minorCollisionShake = 0.25f;

    [Tooltip("Camera shake intensity for heavy obstacle collisions (>= 50 damage)")]
    [SerializeField] private float majorCollisionShake = 0.6f;

    [Tooltip("Camera shake intensity when bouncing off the road curb")]
    [SerializeField] private float curbBounceShake = 0.2f;

    private VehicleHealth health;
    private VehicleAudio vehicleAudio;
    private VehicleCollisionHandler collisionHandler;
    private VehicleMovement movement;

    private bool isJumping = false;

    private void Awake()
    {
        if (health == null) health = GetComponent<VehicleHealth>();
        if (vehicleAudio == null) vehicleAudio = GetComponent<VehicleAudio>();
        if (collisionHandler == null) collisionHandler = GetComponent<VehicleCollisionHandler>();
        if (movement == null) movement = GetComponent<VehicleMovement>();

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

        if (movement != null)
        {
            movement.OnCurbContact += HandleCurbContact;
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

        if (movement != null)
        {
            movement.OnCurbContact -= HandleCurbContact;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDeliveryStarted -= ResetVFX;
            GameManager.Instance.OnOrderSelectionStarted -= ResetVFX;
        }
    }

    private void HandleDamageTaken(int dmg)
    {
        // Major collision explosion particle effect
        if (dmg >= 50)
        {
            PlayParticle(explosionEffect);
        }

        // Proportional impact camera shake
        if (CameraShake.Instance != null && dmg > 0)
        {
            float shakePower = dmg >= 50 ? majorCollisionShake : minorCollisionShake;
            CameraShake.Instance.Shake(0.3f, shakePower);
        }
    }

    private void HandleObstacleBump()
    {
        TriggerBumpJump();
    }

    private void HandleCurbContact()
    {
        if (CameraShake.Instance != null && curbBounceShake > 0f)
        {
            CameraShake.Instance.Shake(0.12f, curbBounceShake);
        }
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
