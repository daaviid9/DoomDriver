using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoomDriver.Environment
{
    /// <summary>
    /// Manages dynamic weather atmosphere presets (rain particles on windshield, volumetric road fog,
    /// wet road reflections, and lighting) linked to the daily shift briefing.
    /// Adheres to AGENTS.md: decoupled C# events, New Input System only, zero Update allocations.
    /// </summary>
    public class WeatherManager : MonoBehaviour
    {
        public static WeatherManager Instance { get; private set; }

        [Header("Weather Presets")]
        [Tooltip("Optional ScriptableObject presets. Missing conditions fall back to built-in defaults.")]
        [SerializeField] private WeatherPreset[] presets;

        [Header("Daily Shift Weather Settings")]
        [Tooltip("Initial weather condition if randomizeOnStart is disabled.")]
        [SerializeField] private WeatherCondition defaultCondition = WeatherCondition.LightRain;

        [Tooltip("If enabled, automatically selects a weather condition for each shift / scene boot.")]
        [SerializeField] private bool randomizeOnStart = false;

        [Tooltip("If enabled, alternates weather automatically when returning to the lobby after a delivery.")]
        [SerializeField] private bool alternateWeatherPerShift = true;

        [Tooltip("Smooth transition duration in seconds when changing weather conditions.")]
        [SerializeField] private float transitionDuration = 2.0f;

        [Header("Scene References (Auto-Resolved if Empty)")]
        [SerializeField] private WindshieldRainEffect windshieldRain;
        [SerializeField] private Light directionalLight;
        [SerializeField] private Camera mainCamera;

        [Header("Road Materials & Wet Reflection Preset")]
        [Tooltip("Primary scene road material (e.g. Ground.mat) switched or blended when wet.")]
        [SerializeField] private Material dryRoadMaterial;
        [Tooltip("Wet asphalt reflection preset material (e.g. Road_Wet.mat).")]
        [SerializeField] private Material wetRoadMaterial;
        [Tooltip("Optional scene ground/road MeshRenderer to swap or tint.")]
        [SerializeField] private MeshRenderer sceneRoadRenderer;

        [Header("Car Windshield Glass Rain Shader")]
        [Tooltip("Optional MeshRenderer for CarConstruction (auto-found by name 'CarConstruction' if empty).")]
        [SerializeField] private MeshRenderer carConstructionRenderer;
        [Tooltip("Material slot index for the car glass on CarConstruction (Element 5).")]
        [SerializeField] private int carGlassMaterialIndex = 5;
        [Tooltip("Fallback reference to WindshieldRainGlass material if needed.")]
        [SerializeField] private Material windshieldGlassMaterial;

        [Header("Debug")]
        [Tooltip("Allows cycling weather presets with F7 during playtesting.")]
        [SerializeField] private bool enableDebugCycleKey = true;

        // Public read-only state for Daily Shift Briefing UI and other observers
        public WeatherCondition CurrentCondition { get; private set; } = WeatherCondition.Clear;
        public WeatherPreset CurrentPreset { get; private set; }
        public string CurrentBriefingMessage => CurrentPreset != null ? CurrentPreset.BriefingHeadline : string.Empty;
        public int CurrentShiftIndex { get; private set; } = 1;

        /// <summary>
        /// Fired whenever the active weather condition changes (e.g. ShiftBriefingUI can display the headline).
        /// </summary>
        public event Action<WeatherCondition, WeatherPreset> OnWeatherChanged;

        // Cached runtime state
        private readonly Dictionary<WeatherCondition, WeatherPreset> presetLookup = new Dictionary<WeatherCondition, WeatherPreset>();
        private readonly List<Renderer> trackedRoadRenderers = new List<Renderer>(64);
        private MaterialPropertyBlock roadPropertyBlock;
        private Material runtimeGlassMaterial;
        private float accumulatedGlassFlowTime = 0f;
        private float accumulatedSideDownTime = 0f;
        private float accumulatedSideBackTime = 0f;

        private float baseDirectionalIntensity = 1f;
        private Color baseCameraBackgroundColor = new Color(0.1415f, 0.1415f, 0.1415f, 1f);

        // Transition interpolation state
        private bool isTransitioning;
        private float transitionTimer;

        private float startFogDensity;
        private float targetFogDensity;
        private Color startFogColor;
        private Color targetFogColor;

        private float startRainEmission;
        private float targetRainEmission;
        private float startRainSpeed;
        private float targetRainSpeed;
        private Color startRainTint;
        private Color targetRainTint;

        private float startRoadSmoothness;
        private float targetRoadSmoothness;
        private float startRoadMetallic;
        private float targetRoadMetallic;
        private Color startRoadTint;
        private Color targetRoadTint;

        private float startLightMult;
        private float targetLightMult;
        private Color startSkyBg;
        private Color targetSkyBg;

        private float currentSmoothness = 0.2f;
        private float currentMetallic = 0f;
        private Color currentRoadTint = Color.white;
        private float currentRainEmission = 0f;
        private float currentRainSpeed = 1f;
        private Color currentRainTint = Color.white;
        private float currentLightMult = 1f;
        private Color currentSkyBg;

        // Cached shader property IDs (zero string hashing in Update)
        private static readonly int SmoothnessPropId = Shader.PropertyToID("_Smoothness");
        private static readonly int GlossinessPropId = Shader.PropertyToID("_Glossiness");
        private static readonly int MetallicPropId = Shader.PropertyToID("_Metallic");
        private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropId = Shader.PropertyToID("_Color");
        private static readonly int RainIntensityPropId = Shader.PropertyToID("_RainIntensity");
        private static readonly int DropSpeedPropId = Shader.PropertyToID("_DropSpeed");
        private static readonly int FlowTimePropId = Shader.PropertyToID("_FlowTime");
        private static readonly int SideDownTimePropId = Shader.PropertyToID("_SideDownTime");
        private static readonly int SideBackTimePropId = Shader.PropertyToID("_SideBackTime");
        private static readonly int SpeedFactorPropId = Shader.PropertyToID("_SpeedFactor");

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

            roadPropertyBlock = new MaterialPropertyBlock();
            BuildPresetLookup();
            ResolveSceneReferences();
        }

        private void Start()
        {
            SubscribeToEvents();
            RefreshExistingChunks();

            WeatherCondition initialCondition = randomizeOnStart
                ? GetRandomWeatherCondition()
                : defaultCondition;

            ApplyWeather(initialCondition, instant: true);
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (enableDebugCycleKey && Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame)
            {
                CycleWeather();
            }

            // Accumulate windshield & side-window raindrop flow smoothly:
            // - Front windshield: parked/braking -> down (+), accelerating -> up/back (-)
            // - Side windows: parked/braking -> down (-Y), accelerating -> horizontally backward (-Z)
            if (runtimeGlassMaterial != null && currentRainEmission > 0.5f)
            {
                float speed = WorldSpeedController.Instance != null ? WorldSpeedController.Instance.CurrentSpeed : 0f;
                float speedNorm = Mathf.Clamp01(speed / 26f);

                // Front windshield flow
                float frontFlowVelocity = Mathf.Lerp(0.85f, -1.45f, speedNorm) * currentRainSpeed;
                accumulatedGlassFlowTime += Time.deltaTime * frontFlowVelocity;

                // Side windows: downward when slow, horizontal backward (-Z) when accelerating
                accumulatedSideDownTime += Time.deltaTime * Mathf.Lerp(0.85f, 0.25f, speedNorm) * currentRainSpeed;
                accumulatedSideBackTime += Time.deltaTime * Mathf.Lerp(0.25f, 2.20f, speedNorm) * currentRainSpeed;

                runtimeGlassMaterial.SetFloat(FlowTimePropId, accumulatedGlassFlowTime);
                runtimeGlassMaterial.SetFloat(SideDownTimePropId, accumulatedSideDownTime);
                runtimeGlassMaterial.SetFloat(SideBackTimePropId, accumulatedSideBackTime);
                runtimeGlassMaterial.SetFloat(SpeedFactorPropId, speedNorm);
            }

            if (!isTransitioning) return;

            transitionTimer += Time.deltaTime;
            float t = transitionDuration > 0.001f
                ? Mathf.Clamp01(transitionTimer / transitionDuration)
                : 1f;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            ApplyInterpolatedState(smoothT);

            if (t >= 1f)
            {
                isTransitioning = false;
            }
        }

        /// <summary>
        /// Applies a specific weather condition with smooth or instant transition.
        /// </summary>
        public void ApplyWeather(WeatherCondition condition, bool instant = false)
        {
            if (!presetLookup.TryGetValue(condition, out WeatherPreset preset) || preset == null)
            {
                preset = WeatherPreset.CreateRuntimeDefault(condition);
                presetLookup[condition] = preset;
            }

            CurrentCondition = condition;
            CurrentPreset = preset;

            // Snapshot current values as transition start
            startFogDensity = RenderSettings.fog ? RenderSettings.fogDensity : 0f;
            startFogColor = RenderSettings.fogColor;
            startRainEmission = currentRainEmission;
            startRainSpeed = currentRainSpeed;
            startRainTint = currentRainTint;
            startRoadSmoothness = currentSmoothness;
            startRoadMetallic = currentMetallic;
            startRoadTint = currentRoadTint;
            startLightMult = currentLightMult;
            startSkyBg = currentSkyBg;

            // Set targets from preset (and wet road material if assigned)
            targetFogDensity = preset.EnableFog ? preset.FogDensity : 0f;
            targetFogColor = preset.FogColor;
            targetRainEmission = preset.EnableRain ? preset.RainEmissionRate : 0f;
            targetRainSpeed = preset.RainSpeedMultiplier;
            targetRainTint = preset.RainTint;

            if (preset.WetRoad && wetRoadMaterial != null)
            {
                targetRoadSmoothness = wetRoadMaterial.HasProperty(SmoothnessPropId)
                    ? wetRoadMaterial.GetFloat(SmoothnessPropId)
                    : preset.RoadSmoothness;
                targetRoadMetallic = wetRoadMaterial.HasProperty(MetallicPropId)
                    ? wetRoadMaterial.GetFloat(MetallicPropId)
                    : preset.RoadMetallic;
            }
            else
            {
                targetRoadSmoothness = preset.RoadSmoothness;
                targetRoadMetallic = preset.RoadMetallic;
            }

            targetRoadTint = preset.RoadTintMultiplier;
            targetLightMult = preset.DirectionalLightMultiplier;
            targetSkyBg = preset.EnableFog ? preset.FogColor : baseCameraBackgroundColor;

            // Apply wet/dry material preset swap on the scene road renderer if available
            if (sceneRoadRenderer != null)
            {
                if (preset.WetRoad && wetRoadMaterial != null)
                {
                    sceneRoadRenderer.sharedMaterial = wetRoadMaterial;
                }
                else if (!preset.WetRoad && dryRoadMaterial != null)
                {
                    sceneRoadRenderer.sharedMaterial = dryRoadMaterial;
                }
            }

            RenderSettings.fogMode = preset.FogMode;

            if (instant || transitionDuration <= 0.001f)
            {
                isTransitioning = false;
                ApplyInterpolatedState(1f);
            }
            else
            {
                transitionTimer = 0f;
                isTransitioning = true;
            }

            Debug.Log($"[WeatherManager] Applied weather: {preset.DisplayName} ({condition}) | FogDensity={targetFogDensity:F3} | Rain={targetRainEmission:F0} | WetRoad={preset.WetRoad}");
            OnWeatherChanged?.Invoke(CurrentCondition, CurrentPreset);
        }

        /// <summary>
        /// Parses a daily shift briefing message or weather keyword (e.g. "Rainy Tuesday", "Heavy Fog", "Clear")
        /// and adjusts fog density, rain particles, and road wetness accordingly.
        /// Designed for direct integration with Dev 5's ShiftBriefingUI.
        /// </summary>
        public WeatherCondition ApplyBriefingCondition(string briefingText, bool instant = false)
        {
            WeatherCondition parsed = ParseConditionFromBriefing(briefingText);
            ApplyWeather(parsed, instant);
            return parsed;
        }

        /// <summary>
        /// Selects a deterministic or random weather condition for a given shift day index.
        /// </summary>
        public WeatherCondition SelectDailyWeather(int shiftDayIndex, bool instant = false)
        {
            CurrentShiftIndex = Mathf.Max(1, shiftDayIndex);
            WeatherCondition chosen = (WeatherCondition)((CurrentShiftIndex - 1) % 4);
            ApplyWeather(chosen, instant);
            return chosen;
        }

        /// <summary>
        /// Cycles to the next weather preset (Clear -> Foggy -> LightRain -> RainAndFog).
        /// </summary>
        [ContextMenu("Cycle Weather Preset")]
        public void CycleWeather()
        {
            int next = ((int)CurrentCondition + 1) % 4;
            ApplyWeather((WeatherCondition)next, instant: false);
        }

        /// <summary>
        /// Maps a dispatcher briefing text to the appropriate WeatherCondition.
        /// </summary>
        public static WeatherCondition ParseConditionFromBriefing(string briefingText)
        {
            if (string.IsNullOrWhiteSpace(briefingText))
            {
                return WeatherCondition.Clear;
            }

            string lower = briefingText.ToLowerInvariant();
            bool hasRain = lower.Contains("rain") || lower.Contains("drizzle") || lower.Contains("wet") || lower.Contains("storm") || lower.Contains("shower");
            bool hasFog = lower.Contains("fog") || lower.Contains("mist") || lower.Contains("haze") || lower.Contains("visibility");

            if (hasRain && hasFog) return WeatherCondition.RainAndFog;
            if (hasRain) return WeatherCondition.LightRain;
            if (hasFog) return WeatherCondition.Foggy;
            return WeatherCondition.Clear;
        }

        private void ApplyInterpolatedState(float t)
        {
            // 1. Volumetric-style road fog
            float fogDensity = Mathf.Lerp(startFogDensity, targetFogDensity, t);
            Color fogColor = Color.Lerp(startFogColor, targetFogColor, t);
            RenderSettings.fog = fogDensity > 0.0005f;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = fogColor;

            // 2. Windshield rain particles
            currentRainEmission = Mathf.Lerp(startRainEmission, targetRainEmission, t);
            currentRainSpeed = Mathf.Lerp(startRainSpeed, targetRainSpeed, t);
            currentRainTint = Color.Lerp(startRainTint, targetRainTint, t);

            if (windshieldRain != null)
            {
                windshieldRain.SetRainParameters(currentRainEmission, currentRainSpeed, currentRainTint);
            }

            // 2b. Car windshield & side window raindrop glass shader (CarConstruction Element 5)
            if (runtimeGlassMaterial != null)
            {
                float normalizedRain = Mathf.Clamp01(currentRainEmission / 220f);
                runtimeGlassMaterial.SetFloat(RainIntensityPropId, normalizedRain);
            }

            // 3. Wet road reflections
            currentSmoothness = Mathf.Lerp(startRoadSmoothness, targetRoadSmoothness, t);
            currentMetallic = Mathf.Lerp(startRoadMetallic, targetRoadMetallic, t);
            currentRoadTint = Color.Lerp(startRoadTint, targetRoadTint, t);
            ApplyRoadReflectionPropertiesToTrackedRenderers();

            // 4. Directional lighting & horizon background blend
            currentLightMult = Mathf.Lerp(startLightMult, targetLightMult, t);
            if (directionalLight != null)
            {
                directionalLight.intensity = baseDirectionalIntensity * currentLightMult;
            }

            currentSkyBg = Color.Lerp(startSkyBg, targetSkyBg, t);
            if (mainCamera != null && mainCamera.clearFlags == CameraClearFlags.SolidColor)
            {
                mainCamera.backgroundColor = currentSkyBg;
            }
        }

        private void ApplyRoadReflectionPropertiesToTrackedRenderers()
        {
            // Clean destroyed entries in reverse
            for (int i = trackedRoadRenderers.Count - 1; i >= 0; i--)
            {
                Renderer rend = trackedRoadRenderers[i];
                if (rend == null)
                {
                    trackedRoadRenderers.RemoveAt(i);
                    continue;
                }

                ApplyRoadPropertiesToRenderer(rend);
            }
        }

        private void ApplyRoadPropertiesToRenderer(Renderer rend)
        {
            if (rend == null) return;

            rend.GetPropertyBlock(roadPropertyBlock);
            roadPropertyBlock.SetFloat(SmoothnessPropId, currentSmoothness);
            roadPropertyBlock.SetFloat(GlossinessPropId, currentSmoothness);
            roadPropertyBlock.SetFloat(MetallicPropId, currentMetallic);
            roadPropertyBlock.SetColor(BaseColorPropId, currentRoadTint);
            roadPropertyBlock.SetColor(ColorPropId, currentRoadTint);
            rend.SetPropertyBlock(roadPropertyBlock);
        }

        private void RegisterChunkRoadRenderers(RoadChunk chunk)
        {
            if (chunk == null) return;

            Transform roadGroup = chunk.transform.Find("Road");
            Transform searchRoot = roadGroup != null ? roadGroup : chunk.transform;

            MeshRenderer[] renderers = searchRoot.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer mr = renderers[i];
                if (mr == null) continue;

                // If searching the full chunk, filter to road-named objects only so buildings stay matte
                if (roadGroup == null && mr.gameObject.name.IndexOf("Road", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (!trackedRoadRenderers.Contains(mr))
                {
                    trackedRoadRenderers.Add(mr);
                    ApplyRoadPropertiesToRenderer(mr);
                }
            }
        }

        private void UnregisterChunkRoadRenderers(RoadChunk chunk)
        {
            if (chunk == null) return;

            MeshRenderer[] renderers = chunk.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                trackedRoadRenderers.Remove(renderers[i]);
            }
        }

        private void RefreshExistingChunks()
        {
            if (EnvironmentSpawner.Instance == null) return;

            var chunks = EnvironmentSpawner.Instance.ActiveChunks;
            for (int i = 0; i < chunks.Count; i++)
            {
                if (chunks[i] != null && chunks[i].TryGetComponent<RoadChunk>(out var roadChunk))
                {
                    RegisterChunkRoadRenderers(roadChunk);
                }
            }
        }

        private void HandleOrderSelectionStarted()
        {
            // If DailyShiftBriefing is active, it owns shift-day progression and triggers SelectDailyWeather.
            if (!alternateWeatherPerShift || DoomDriver.Core.DailyShiftBriefing.Instance != null) return;

            CurrentShiftIndex++;
            if (CurrentShiftIndex > 1)
            {
                CycleWeather();
            }
        }

        private void BuildPresetLookup()
        {
            presetLookup.Clear();

            if (presets != null)
            {
                for (int i = 0; i < presets.Length; i++)
                {
                    if (presets[i] != null)
                    {
                        presetLookup[presets[i].Condition] = presets[i];
                    }
                }
            }

            // Guarantee all 4 conditions always have a valid preset
            foreach (WeatherCondition cond in Enum.GetValues(typeof(WeatherCondition)))
            {
                if (!presetLookup.ContainsKey(cond))
                {
                    presetLookup[cond] = WeatherPreset.CreateRuntimeDefault(cond);
                }
            }
        }

        private void ResolveSceneReferences()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (mainCamera != null)
            {
                baseCameraBackgroundColor = mainCamera.backgroundColor;
                currentSkyBg = baseCameraBackgroundColor;
            }

            if (directionalLight == null)
            {
                Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i].type == LightType.Directional)
                    {
                        directionalLight = lights[i];
                        break;
                    }
                }
            }

            if (directionalLight != null)
            {
                baseDirectionalIntensity = directionalLight.intensity;
            }

            if (windshieldRain == null && mainCamera != null)
            {
                windshieldRain = mainCamera.GetComponentInChildren<WindshieldRainEffect>(true);
                if (windshieldRain == null)
                {
                    GameObject rainObj = new GameObject("WindshieldRainParticles");
                    rainObj.transform.SetParent(mainCamera.transform, false);
                    windshieldRain = rainObj.AddComponent<WindshieldRainEffect>();
                }
            }

            if (sceneRoadRenderer == null)
            {
                GameObject roadObj = GameObject.Find("Road");
                if (roadObj != null)
                {
                    sceneRoadRenderer = roadObj.GetComponent<MeshRenderer>();
                    if (dryRoadMaterial == null && sceneRoadRenderer != null)
                    {
                        dryRoadMaterial = sceneRoadRenderer.sharedMaterial;
                    }
                }
            }

            ResolveCarWindshieldGlass();
        }

        private void ResolveCarWindshieldGlass()
        {
            if (carConstructionRenderer == null)
            {
                GameObject carConstruction = GameObject.Find("CarConstruction");
                if (carConstruction != null)
                {
                    carConstructionRenderer = carConstruction.GetComponent<MeshRenderer>();
                }
            }

            if (carConstructionRenderer != null)
            {
                Material[] mats = carConstructionRenderer.materials; // creates clean runtime instances
                if (mats != null && carGlassMaterialIndex >= 0 && carGlassMaterialIndex < mats.Length)
                {
                    Material slotMat = mats[carGlassMaterialIndex];
                    if (slotMat != null && slotMat.HasProperty(RainIntensityPropId))
                    {
                        runtimeGlassMaterial = slotMat;
                    }
                    else if (windshieldGlassMaterial != null)
                    {
                        runtimeGlassMaterial = new Material(windshieldGlassMaterial);
                        mats[carGlassMaterialIndex] = runtimeGlassMaterial;
                        carConstructionRenderer.materials = mats;
                    }
                }
            }
        }

        private void SubscribeToEvents()
        {
            if (EnvironmentSpawner.Instance != null)
            {
                EnvironmentSpawner.Instance.OnChunkSpawned += RegisterChunkRoadRenderers;
                EnvironmentSpawner.Instance.OnChunkDespawned += UnregisterChunkRoadRenderers;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnOrderSelectionStarted += HandleOrderSelectionStarted;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (EnvironmentSpawner.Instance != null)
            {
                EnvironmentSpawner.Instance.OnChunkSpawned -= RegisterChunkRoadRenderers;
                EnvironmentSpawner.Instance.OnChunkDespawned -= UnregisterChunkRoadRenderers;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnOrderSelectionStarted -= HandleOrderSelectionStarted;
            }
        }

        private static WeatherCondition GetRandomWeatherCondition()
        {
            return (WeatherCondition)UnityEngine.Random.Range(0, 4);
        }
    }
}
