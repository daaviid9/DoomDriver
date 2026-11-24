using UnityEngine;

namespace DoomDriver.Environment
{
    /// <summary>
    /// Controls the light rain particle system attached to the Main Camera in front of the windshield.
    /// Dynamically adjusts rain emission, tint, and streak velocity based on active weather and vehicle speed.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class WindshieldRainEffect : MonoBehaviour
    {
        [Header("Particle Configuration")]
        [SerializeField] private ParticleSystem rainParticles;
        [SerializeField] private Material rainMaterial;
        [SerializeField] private Vector3 localEmitterOffset = new Vector3(0f, 2.4f, 5.5f);
        [SerializeField] private Vector3 emitterBoxSize = new Vector3(11f, 1.5f, 10f);

        [Header("Dynamic Driving Response")]
        [Tooltip("Base downward falling speed of raindrops.")]
        [SerializeField] private float baseFallSpeed = 14f;
        [Tooltip("How much forward driving speed (WorldMover.moveSpeed) slants rain streaks toward the windshield.")]
        [SerializeField] private float forwardSpeedSlantFactor = 0.65f;

        private ParticleSystem.EmissionModule emissionModule;
        private ParticleSystem.MainModule mainModule;
        private ParticleSystem.VelocityOverLifetimeModule velocityModule;
        private ParticleSystemRenderer particleRenderer;

        private float currentEmissionRate;
        private float currentSpeedMultiplier = 1f;
        private bool isInitialized;

        private void Awake()
        {
            InitializeIfNeeded();
        }

        private void Update()
        {
            if (!isInitialized || rainParticles == null || !rainParticles.isPlaying) return;

            // Slant rain backward toward the windshield proportionally to car speed
            float drivingSpeed = WorldMover.moveSpeed;
            velocityModule.z = -(drivingSpeed * forwardSpeedSlantFactor * currentSpeedMultiplier);
        }

        /// <summary>
        /// Ensures the ParticleSystem is properly configured for URP windshield rain streaks.
        /// </summary>
        public void InitializeIfNeeded()
        {
            if (isInitialized) return;

            if (rainParticles == null)
            {
                rainParticles = GetComponent<ParticleSystem>();
            }

            if (rainParticles == null)
            {
                rainParticles = gameObject.AddComponent<ParticleSystem>();
            }

            particleRenderer = rainParticles.GetComponent<ParticleSystemRenderer>();
            mainModule = rainParticles.main;
            emissionModule = rainParticles.emission;
            velocityModule = rainParticles.velocityOverLifetime;

            ConfigureParticleSystem();
            isInitialized = true;
        }

        /// <summary>
        /// Applies target rain emission, speed multiplier, and color tint.
        /// </summary>
        public void SetRainParameters(float emissionRate, float speedMultiplier, Color tint)
        {
            InitializeIfNeeded();
            if (rainParticles == null) return;

            currentEmissionRate = Mathf.Max(0f, emissionRate);
            currentSpeedMultiplier = Mathf.Max(0.25f, speedMultiplier);

            emissionModule.rateOverTime = currentEmissionRate;
            mainModule.startSpeed = baseFallSpeed * currentSpeedMultiplier;
            mainModule.startColor = tint;

            if (currentEmissionRate > 0.5f)
            {
                if (!rainParticles.isPlaying)
                {
                    rainParticles.Play();
                }
            }
            else
            {
                if (rainParticles.isPlaying)
                {
                    rainParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        private void ConfigureParticleSystem()
        {
            transform.localPosition = localEmitterOffset;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            mainModule.loop = true;
            mainModule.playOnAwake = false;
            mainModule.simulationSpace = ParticleSystemSimulationSpace.Local;
            mainModule.startLifetime = 0.75f;
            mainModule.startSpeed = baseFallSpeed;
            mainModule.startSize = 0.045f;
            mainModule.maxParticles = 600;
            mainModule.startColor = new Color(0.78f, 0.86f, 0.95f, 0.45f);
            mainModule.gravityModifier = 0f;

            emissionModule.enabled = true;
            emissionModule.rateOverTime = 0f;

            var shape = rainParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = emitterBoxSize;
            shape.rotation = new Vector3(90f, 0f, 0f); // Emit downward along -Y

            velocityModule.enabled = true;
            velocityModule.space = ParticleSystemSimulationSpace.Local;
            velocityModule.x = 0f;
            velocityModule.y = -2f;
            velocityModule.z = -3f;

            if (particleRenderer != null)
            {
                particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                particleRenderer.lengthScale = 6.5f;
                particleRenderer.velocityScale = 0.06f;
                particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                particleRenderer.receiveShadows = false;

                if (rainMaterial != null)
                {
                    particleRenderer.sharedMaterial = rainMaterial;
                }
                else if (particleRenderer.sharedMaterial == null)
                {
                    Shader urpParticles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                    if (urpParticles == null) urpParticles = Shader.Find("Sprites/Default");
                    if (urpParticles != null)
                    {
                        var runtimeMat = new Material(urpParticles)
                        {
                            name = "RainStreak_Runtime"
                        };
                        runtimeMat.SetFloat("_Surface", 1f); // Transparent
                        runtimeMat.SetColor("_BaseColor", new Color(0.8f, 0.88f, 0.96f, 0.45f));
                        particleRenderer.sharedMaterial = runtimeMat;
                    }
                }
            }
        }
    }
}
