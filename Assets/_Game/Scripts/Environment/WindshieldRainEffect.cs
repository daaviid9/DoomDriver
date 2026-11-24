using UnityEngine;

namespace DoomDriver.Environment
{
    /// <summary>
    /// Controls both:
    /// 1) Exterior falling rain streaks (strictly outside the car cabin, never entering the interior).
    /// 2) Windshield glass water droplets that appear, trickle, and fade on the front glass.
    /// Attached to the Main Camera and driven by WeatherManager.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class WindshieldRainEffect : MonoBehaviour
    {
        [Header("Exterior Rain (Outside Car Only)")]
        [SerializeField] private ParticleSystem rainParticles;
        [SerializeField] private Material rainMaterial;
        [Tooltip("Emitter offset in Main Camera local space. Kept well ahead of the hood so rain stays outside.")]
        [SerializeField] private Vector3 localEmitterOffset = new Vector3(0f, 5.0f, 15.5f);
        [Tooltip("Volume size for exterior rain emission.")]
        [SerializeField] private Vector3 emitterBoxSize = new Vector3(22f, 1.5f, 18f);
        [Tooltip("Minimum local Z distance from camera allowed for exterior raindrops (car hood ends around Z = 3.6m).")]
        [SerializeField] private float exteriorMinZ = 4.2f;

        [Header("Windshield Glass Droplets (Legacy Billboard Fallback)")]
        [Tooltip("Disabled by default because droplets now render directly on CarConstruction Element 5 glass shader.")]
        [SerializeField] private bool enableWindshieldDroplets = false;
        [Tooltip("Position of the windshield glass plane in Main Camera local space (behind phone, in front of dashboard).")]
        [SerializeField] private Vector3 windshieldLocalOffset = new Vector3(0f, 0.10f, 1.15f);
        [Tooltip("Width and height of the windshield droplet area.")]
        [SerializeField] private Vector2 windshieldAreaSize = new Vector2(1.55f, 0.72f);
        [Tooltip("Multiplier of rain emission rate used for windshield droplets.")]
        [Range(0.05f, 1.5f)]
        [SerializeField] private float dropletEmissionMultiplier = 0.28f;
        [Tooltip("Min/Max world size of individual droplets on the windshield.")]
        [SerializeField] private Vector2 dropletSizeRange = new Vector2(0.018f, 0.045f);

        [Header("Dynamic Driving Response")]
        [Tooltip("Base downward falling speed of exterior raindrops.")]
        [SerializeField] private float baseFallSpeed = 15f;
        [Tooltip("How much forward driving speed (WorldSpeedController.CurrentSpeed) slants exterior rain streaks.")]
        [SerializeField] private float forwardSpeedSlantFactor = 0.45f;

        // Exterior rain modules
        private ParticleSystem.EmissionModule emissionModule;
        private ParticleSystem.MainModule mainModule;
        private ParticleSystem.VelocityOverLifetimeModule velocityModule;
        private ParticleSystemRenderer particleRenderer;
        private ParticleSystem.Particle[] particleBuffer;

        // Windshield droplet modules
        private ParticleSystem dropletParticles;
        private ParticleSystem.EmissionModule dropletEmission;
        private ParticleSystem.MainModule dropletMain;
        private ParticleSystem.VelocityOverLifetimeModule dropletVelocity;
        private Material dropletMaterial;
        private Texture2D dropletTexture;

        private float currentEmissionRate;
        private float currentSpeedMultiplier = 1f;
        private bool isInitialized;

        private void Awake()
        {
            InitializeIfNeeded();
        }

        private void OnDestroy()
        {
            if (dropletMaterial != null) Destroy(dropletMaterial);
            if (dropletTexture != null) Destroy(dropletTexture);
        }

        private void Update()
        {
            if (!isInitialized) return;

            float drivingSpeed = WorldSpeedController.Instance != null ? WorldSpeedController.Instance.CurrentSpeed : 0f;

            if (rainParticles != null && rainParticles.isPlaying)
            {
                // Slant exterior rain slightly toward the car based on forward speed
                velocityModule.z = -(drivingSpeed * forwardSpeedSlantFactor * currentSpeedMultiplier);
            }

            if (dropletParticles != null && dropletParticles.isPlaying)
            {
                // Slow downward trickle when parked, slight upward airflow push at highway speed
                float trickleY = Mathf.Lerp(-0.06f, 0.035f, Mathf.Clamp01(drivingSpeed / 25f));
                dropletVelocity.y = trickleY;
            }
        }

        private void LateUpdate()
        {
            if (!isInitialized || rainParticles == null || !rainParticles.isPlaying) return;

            // Guarantee NO exterior raindrop ever enters the car cabin (local Z < exteriorMinZ)
            int maxCount = mainModule.maxParticles;
            if (particleBuffer == null || particleBuffer.Length < maxCount)
            {
                particleBuffer = new ParticleSystem.Particle[maxCount];
            }

            int aliveCount = rainParticles.GetParticles(particleBuffer);
            bool modified = false;

            // Convert exteriorMinZ from camera space to emitter local space
            float minLocalZ = exteriorMinZ - transform.localPosition.z;

            for (int i = 0; i < aliveCount; i++)
            {
                if (particleBuffer[i].position.z < minLocalZ)
                {
                    particleBuffer[i].remainingLifetime = 0f;
                    modified = true;
                }
            }

            if (modified)
            {
                rainParticles.SetParticles(particleBuffer, aliveCount);
            }
        }

        /// <summary>
        /// Ensures both the exterior rain and windshield droplet particle systems are configured for URP.
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

            ConfigureExteriorRainSystem();

            if (enableWindshieldDroplets)
            {
                ConfigureWindshieldDropletsSystem();
            }

            isInitialized = true;
        }

        /// <summary>
        /// Applies target rain emission, speed multiplier, and color tint to both exterior rain and windshield droplets.
        /// </summary>
        public void SetRainParameters(float emissionRate, float speedMultiplier, Color tint)
        {
            InitializeIfNeeded();
            if (rainParticles == null) return;

            currentEmissionRate = Mathf.Max(0f, emissionRate);
            currentSpeedMultiplier = Mathf.Max(0.25f, speedMultiplier);

            // 1. Exterior rain
            emissionModule.rateOverTime = currentEmissionRate;
            mainModule.startSpeed = baseFallSpeed * currentSpeedMultiplier;
            mainModule.startColor = tint;

            if (currentEmissionRate > 0.5f)
            {
                if (!rainParticles.isPlaying) rainParticles.Play();
            }
            else
            {
                if (rainParticles.isPlaying) rainParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            // 2. Windshield glass droplets
            if (dropletParticles != null)
            {
                float dropletRate = enableWindshieldDroplets ? currentEmissionRate * dropletEmissionMultiplier : 0f;
                dropletEmission.rateOverTime = dropletRate;

                Color dropletColor = new Color(
                    Mathf.Min(1f, tint.r + 0.15f),
                    Mathf.Min(1f, tint.g + 0.12f),
                    Mathf.Min(1f, tint.b + 0.08f),
                    Mathf.Clamp(tint.a + 0.25f, 0.35f, 0.80f));
                dropletMain.startColor = dropletColor;

                if (dropletRate > 0.5f)
                {
                    if (!dropletParticles.isPlaying) dropletParticles.Play();
                }
                else
                {
                    if (dropletParticles.isPlaying) dropletParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        private void ConfigureExteriorRainSystem()
        {
            transform.localPosition = localEmitterOffset;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            mainModule.loop = true;
            mainModule.playOnAwake = false;
            mainModule.simulationSpace = ParticleSystemSimulationSpace.Local;
            mainModule.startLifetime = 0.85f;
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
            velocityModule.z = -1.5f;

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
                    particleRenderer.sharedMaterial = CreateUrpParticleMaterial("RainStreak_Runtime", null, new Color(0.8f, 0.88f, 0.96f, 0.45f));
                }
            }
        }

        private void ConfigureWindshieldDropletsSystem()
        {
            Transform parentCam = transform.parent != null ? transform.parent : transform;
            Transform existing = parentCam.Find("WindshieldGlassDroplets");
            GameObject dropletObj = existing != null ? existing.gameObject : new GameObject("WindshieldGlassDroplets");
            dropletObj.transform.SetParent(parentCam, false);
            dropletObj.transform.localPosition = windshieldLocalOffset;
            dropletObj.transform.localRotation = Quaternion.identity;
            dropletObj.transform.localScale = Vector3.one;

            dropletParticles = dropletObj.GetComponent<ParticleSystem>();
            if (dropletParticles == null)
            {
                dropletParticles = dropletObj.AddComponent<ParticleSystem>();
            }

            dropletMain = dropletParticles.main;
            dropletMain.loop = true;
            dropletMain.playOnAwake = false;
            dropletMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            dropletMain.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.8f);
            dropletMain.startSpeed = 0f;
            dropletMain.startSize = new ParticleSystem.MinMaxCurve(dropletSizeRange.x, dropletSizeRange.y);
            dropletMain.maxParticles = 180;
            dropletMain.startColor = new Color(0.88f, 0.94f, 1.0f, 0.65f);
            dropletMain.gravityModifier = 0f;

            dropletEmission = dropletParticles.emission;
            dropletEmission.enabled = true;
            dropletEmission.rateOverTime = 0f;

            var shape = dropletParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(windshieldAreaSize.x, windshieldAreaSize.y, 0.01f);
            shape.rotation = Vector3.zero;

            dropletVelocity = dropletParticles.velocityOverLifetime;
            dropletVelocity.enabled = true;
            dropletVelocity.space = ParticleSystemSimulationSpace.Local;
            dropletVelocity.x = new ParticleSystem.MinMaxCurve(-0.008f, 0.008f);
            dropletVelocity.y = -0.04f;
            dropletVelocity.z = 0f;

            // Smooth pop-in and gradual fade-out on the windshield glass
            var colorOverLifetime = dropletParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fadeGrad = new Gradient();
            fadeGrad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.95f, 0.08f),
                    new GradientAlphaKey(0.85f, 0.65f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fadeGrad);

            var sizeOverLifetime = dropletParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.10f, 1.0f),
                new Keyframe(0.80f, 0.92f),
                new Keyframe(1f, 0.5f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var renderer = dropletParticles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingFudge = -2f;

                dropletTexture = CreateDropletTexture(64);
                dropletMaterial = CreateUrpParticleMaterial("WindshieldDroplet_Runtime", dropletTexture, new Color(0.92f, 0.96f, 1f, 0.72f));
                renderer.sharedMaterial = dropletMaterial;
            }
        }

        private static Material CreateUrpParticleMaterial(string matName, Texture2D mainTex, Color baseColor)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material mat = new Material(shader) { name = matName };
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);     // Alpha blend
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;

            if (mainTex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", mainTex);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);

            return mat;
        }

        /// <summary>
        /// Generates a crisp radial water-droplet texture with a soft rim and upper specular glint.
        /// </summary>
        private static Texture2D CreateDropletTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "WindshieldDropletTex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color[] pixels = new Color[size * size];
            float half = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Slightly vertically elongated raindrop silhouette
                    float nx = (x - half) / (half * 0.85f);
                    float ny = (y - half) / (half * 0.96f);
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);

                    if (dist >= 1f)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    // Translucent water center + brighter refractive rim + top-left specular highlight
                    float edgeAlpha = Mathf.SmoothStep(1f, 0.72f, dist);
                    float rimBoost = Mathf.SmoothStep(0.55f, 0.92f, dist) * 0.45f;

                    float hx = nx + 0.28f;
                    float hy = ny - 0.30f;
                    float highlight = Mathf.Exp(-(hx * hx + hy * hy) * 14f) * 0.85f;

                    float brightness = Mathf.Clamp01(0.58f + rimBoost + highlight);
                    float alpha = Mathf.Clamp01(edgeAlpha * (0.38f + rimBoost * 0.85f + highlight));

                    pixels[y * size + x] = new Color(brightness * 0.92f, brightness * 0.97f, brightness, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}
