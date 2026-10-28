using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float laneDistance = 3f;
    public float laneChangeSpeed = 15f;

    [Header("Steering Wheel Settings")]
    public Transform steeringWheel;
    public float maxSteeringAngle = 45f;
    public float steeringRotationSpeed = 15f;

    private InputSystem_Actions controls;
    private int targetLane = 1; // 0=Left, 1=Middle, 2=Right
    private Vector2 moveInput;

    [Header("Zdravie (Health)")]
    public int maxHealth = 100;
    private int currentHealth;

    [Header("Damage (Poškodenie)")]
    public int carDamage = 100;
    public int barricadeDamage = 50;
    public int edgeDamage = 30; 

    [Header("Efekty Zničenia (Dýmenie a Výbuch)")]
    [Tooltip("Vylezie jemný dym pri zdraví 50 a menej")]
    public GameObject smokeLightEffect;
    
    [Tooltip("Hustý, rýchly dym pri zdraví 20 a menej")]
    public GameObject smokeHeavyEffect;

    [Tooltip("Explózia pri totálnom zničení (zdravie 0)")]
    public GameObject explosionEffect;

    [Header("Animácia nárazu (Skok)")]
    public float jumpHeight = 0.5f;
    public float jumpDuration = 0.3f;
    private bool isJumping = false;

    [Header("Nakláňanie (Tilt) pri zatáčaní")]
    [Tooltip("Maximálny uhol naklonenia auta pri zmene pruhu")]
    public float maxTiltAngle = 10f;
    [Tooltip("Rýchlosť, akou sa auto vracia do rovnej polohy")]
    public float tiltSmoothing = 10f;
    private float currentTilt = 0f;
    private float lastX;

    [Header("Audio (Zvuky)")]
    public AudioSource engineSource;
    public AudioSource hissingSource;
    public AudioSource screechSource; 
    public AudioClip crashLightClip;
    public AudioClip crashHeavyClip;

    [Header("Engine Pitch Settings")]
    public float minPitch = 0.8f;
    public float maxPitch = 2.2f;

    // This event notifies our UI slider (if present) to shrink
    public event System.Action<int> OnHealthChanged;

    void Start()
    {
        currentHealth = maxHealth;
        lastX = transform.position.x;
    }

    void Awake()
    {
        controls = new InputSystem_Actions();

        // Register for lane switching events
        controls.Player.Move.performed += ctx => OnMove(ctx.ReadValue<Vector2>());

        // Brutal reset: stop all smoke effects so they never appear in the scene
        ForceStopAndHide(smokeLightEffect);
        ForceStopAndHide(smokeHeavyEffect);
        ForceStopAndHide(explosionEffect);
    }

    private void ForceStopAndHide(GameObject obj)
    {
        if (obj == null) return;
        
        // Find all particle systems in the object and its children
        ParticleSystem[] allParticles = obj.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in allParticles)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(); // Okamžité vymazanie už existujúcich guličiek
            var main = ps.main;
            main.playOnAwake = false; // Safety check directly in code
        }

        obj.SetActive(false);
    }

    void OnEnable()
    {
        if (controls != null) controls.Enable();
    }

    void OnDisable()
    {
        if (controls != null) controls.Disable();
    }

    private void OnMove(Vector2 direction)
    {
        if (GameManager.Instance != null && GameManager.Instance.isGameOver) return;

        // Detect discrete lane switch based on X input
        if (direction.x < -0.5f)
        {
            if (targetLane > 0) targetLane--;
        }
        else if (direction.x > 0.5f)
        {
            if (targetLane < 2) targetLane++;
        }
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isGameOver) return;
        if (GameManager.Instance != null && GameManager.Instance.isPaused) return;

        float targetX = (targetLane - 1) * laneDistance;
        float newX = Mathf.Lerp(transform.position.x, targetX, Time.deltaTime * laneChangeSpeed);
        
        // --- TILT CALCULATION ---
        // Determine side movement speed
        float movementDelta = newX - lastX;
        // When moving right (delta > 0), the car tilts left and vice versa
        float targetTilt = 0f;
        
        // SAFETY: avoid division by zero if deltaTime is unexpectedly zero after unpausing
        if (Time.deltaTime > 0.0001f)
        {
            targetTilt = -(movementDelta / Time.deltaTime) * (maxTiltAngle / 2f);
        }

        targetTilt = Mathf.Clamp(targetTilt, -maxTiltAngle, maxTiltAngle);

        // Smoothly interpolate tilt
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSmoothing);
        
        // Apply movement and rotation
        transform.position = new Vector3(newX, transform.position.y, transform.position.z);

        // Steering Wheel Rotation
        if (steeringWheel != null)
        {
            float distanceToTarget = targetX - transform.position.x;
            float targetAngle = 0f;

            // If we are still moving (more than 0.1 from target), keep wheel fully turned
            if (Mathf.Abs(distanceToTarget) > 0.2f)
            {
                // Positive distance means moving right -> wheel turns right (negative Z angle)
                targetAngle = (distanceToTarget < 0) ? -maxSteeringAngle : maxSteeringAngle;
            }

            // Smoothly interpolate to the chosen angle (0 or max)
            Quaternion targetRot = Quaternion.Euler(0, 0, targetAngle);
            steeringWheel.localRotation = Quaternion.Lerp(steeringWheel.localRotation, targetRot, Time.deltaTime * steeringRotationSpeed);
        }

        // --- APPLY TILT & STORE LAST_X ---
        // Apply overall car tilt
        transform.localRotation = Quaternion.Euler(0, 0, currentTilt);
        // Store position for next frame's speed calculation
        lastX = newX;

        // --- DYNAMIC ENGINE PITCH & IDLE VOLUME ---
        // Pitch changes based on how fast the car is moving
        float speedRatio = Mathf.Clamp01(WorldMover.moveSpeed / 25f);
        if (engineSource != null)
        {
            engineSource.pitch = Mathf.Lerp(minPitch, maxPitch, speedRatio);
            engineSource.volume = Mathf.Lerp(0.6f, 1.0f, speedRatio);
        }

        // --- TIRE SCREECH DURING MANEUVER ---
        if (screechSource != null)
        {
            bool isMovingLane = Mathf.Abs(targetX - transform.position.x) > 0.05f;

            if (isMovingLane)
            {
                if (!screechSource.isPlaying) screechSource.Play();
            }
            else
            {
                if (screechSource.isPlaying) screechSource.Stop();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Obstacle"))
        {
            // Get obstacle type and damage from its data component
            ObstacleData data = other.GetComponent<ObstacleData>();
            int damage = 30; // Default

            if (data != null)
            {
                // Assign damage based on the category set in the inspector
                switch (data.obstacleType)
                {
                    case ObstacleType.Car: damage = carDamage; break;
                    case ObstacleType.Barricade: damage = barricadeDamage; break;
                    case ObstacleType.Edge: damage = edgeDamage; break;
                    default: damage = data.damageAmount; break;
                }
            }

            // --- REACTION BASED ON COLLISION TYPE (only immediate effects from your list) ---
            if (damage >= 100)
            {
                // Car collision = puff effect + heavy crash sound
                PlayParticle(explosionEffect);
                if (engineSource != null) engineSource.PlayOneShot(crashHeavyClip);
            }
            else if (damage >= 50)
            {
                // Barricade collision = puff effect + heavy crash sound + speed penalty
                PlayParticle(explosionEffect);
                if (engineSource != null) engineSource.PlayOneShot(crashHeavyClip);
                
                // SIGNIFICANT SLOWDOWN (Barricade slows us by 7)
                WorldMover.moveSpeed -= 7f;
                if (WorldMover.moveSpeed < 5f) WorldMover.moveSpeed = 5f;
                Debug.Log($"<color=orange>[SPOMALENIE]</color> Narazil si do barikády. Rýchlosť klesla na: {WorldMover.moveSpeed}");
            }
            else
            {
                // Edge obstacles = hop + light crash sound + small speed penalty
                if (!isJumping) StartCoroutine(JumpRoutine());
                if (engineSource != null) engineSource.PlayOneShot(crashLightClip);
                
                // SLOWDOWN (Cone slows us by 4)
                WorldMover.moveSpeed -= 4f;
                if (WorldMover.moveSpeed < 5f) WorldMover.moveSpeed = 5f;
                Debug.Log($"<color=orange>[SPOMALENIE]</color> Narazil si do prekážky. Rýchlosť klesla na: {WorldMover.moveSpeed}");
            }

            // Apply damage to the player
            TakeDamage(damage);

            // Destroy the obstacle
            Destroy(other.gameObject); 
        }
    }

    public void TakeDamage(int dmg)
    {
        currentHealth -= dmg;
        Debug.Log($"<color=red>[NÁRAZ]</color> Ubralo mi {dmg} HP. Zostáva mi: {currentHealth} HP.");

        // Spustíme trasenie kamery
        if (CameraShake.Instance != null && dmg > 0)
        {
            float shakePower = dmg >= 50 ? 0.6f : 0.25f;
            CameraShake.Instance.Shake(0.3f, shakePower);
        }
        
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            PlayParticle(explosionEffect);

            if (GameManager.Instance != null) GameManager.Instance.GameOver();
        }
        else
        {
            RefreshLowHealthSmoke();
        }

        OnHealthChanged?.Invoke(currentHealth);
    }

    private void RefreshLowHealthSmoke()
    {
        // 1. CRITICAL STATE (Heavy smoke + hissing)
        if (currentHealth <= 30)
        {
            if (smokeLightEffect != null) smokeLightEffect.SetActive(false);
            if (smokeHeavyEffect != null && !smokeHeavyEffect.activeSelf) PlayParticle(smokeHeavyEffect);
            
            // Spustíme syčanie dymu
            if (hissingSource != null && !hissingSource.isPlaying) 
            {
                hissingSource.loop = true; // Zabezpečíme, že bude syčať stále kým ho nevypneme
                hissingSource.Play();
            }
            return; 
        }
        
        // 2. BAD STATE (Light smoke + hissing)
        if (currentHealth <= 50)
        {
            if (smokeLightEffect != null && !smokeLightEffect.activeSelf) PlayParticle(smokeLightEffect);
            
            // Spustíme syčanie dymu
            if (hissingSource != null && !hissingSource.isPlaying) 
            {
                hissingSource.loop = true;
                hissingSource.Play();
            }
            return;
        }

        // 3. GOOD HEALTH (No smoke)
        if (smokeLightEffect != null) smokeLightEffect.SetActive(false);
        if (smokeHeavyEffect != null) smokeHeavyEffect.SetActive(false);
        if (hissingSource != null && hissingSource.isPlaying) hissingSource.Stop();
    }

    /// <summary>
    /// Resets vehicle health, position, tilt, effects, and audio for a new delivery order.
    /// </summary>
    public void ResetVehicleState()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth);

        targetLane = 1; // Center lane
        currentTilt = 0f;
        lastX = 0f;

        transform.position = new Vector3(0f, transform.position.y, transform.position.z);
        transform.localRotation = Quaternion.identity;

        if (steeringWheel != null)
        {
            steeringWheel.localRotation = Quaternion.identity;
        }

        // Reset damage smoke and explosion effects
        ForceStopAndHide(smokeLightEffect);
        ForceStopAndHide(smokeHeavyEffect);
        ForceStopAndHide(explosionEffect);

        if (hissingSource != null && hissingSource.isPlaying) hissingSource.Stop();
        if (screechSource != null && screechSource.isPlaying) screechSource.Stop();

        // Ensure engine is running
        if (engineSource != null && !engineSource.isPlaying)
        {
            engineSource.Play();
        }
    }

    // Helper function that not only activates the object but forces the smoke to start emitting
    private void PlayParticle(GameObject obj)
    {
        if (obj == null) return;
        
        obj.SetActive(true); // Zapne objekt v hierarchii
        
        // Pokúsi sa nájsť a spustiť Particle System na samotnom objekte
        ParticleSystem ps = obj.GetComponent<ParticleSystem>();
        if (ps != null) 
        {
            ps.Play(true);
        }
        else 
        {
            // Ak je to zložitý prefab (napr. výbuch z viacerých častí), spustí všetky Particle Systémy vnútri
            ParticleSystem childPs = obj.GetComponentInChildren<ParticleSystem>();
            if (childPs != null) childPs.Play(true);
        }
    }

    // Coroutine for "jump" over an obstacle
    private System.Collections.IEnumerator JumpRoutine()
    {
        isJumping = true;
        Vector3 startPos = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / jumpDuration;
            
            // Sinusoidový pohyb (hore a dole)
            float yOffset = Mathf.Sin(normalizedTime * Mathf.PI) * jumpHeight;
            transform.localPosition = new Vector3(startPos.x, startPos.y + yOffset, startPos.z);
            
            yield return null;
        }

        transform.localPosition = startPos;
        isJumping = false;
    }
}
