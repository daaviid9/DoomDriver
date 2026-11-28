using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoomDriver.Environment
{
    public struct WeatherStateSnapshot
    {
        public WeatherCondition Condition;
        public FogMode FogMode;
        public bool EnableFog;
        public float FogDensity;
        public Color FogColor;
        public float RainEmission;
        public float RainSpeed;
        public Color RainTint;
        public float NormalizedRainIntensity;
        public bool WetRoad;
        public float RoadSmoothness;
        public float RoadMetallic;
        public Color RoadTint;
        public float LightMultiplier;
        public Color SkyColor;
        public float TransitionProgress;
    }

    /// <summary>
    /// Manages dynamic weather presets, shift progression, and smooth state interpolation.
    /// Adheres to SoC: publishes WeatherStateSnapshot via OnWeatherUpdated without touching
    /// scene renderers, car shaders, lights, or cameras directly.
    /// </summary>
    public class WeatherManager : MonoBehaviour
    {
        public static WeatherManager Instance { get; private set; }

        [Header("Weather Presets")]
        [SerializeField] private WeatherPreset[] presets;

        [Header("Daily Shift Weather Settings")]
        [SerializeField] private WeatherCondition defaultCondition = WeatherCondition.LightRain;
        [SerializeField] private bool randomizeOnStart = false;
        [SerializeField] private bool alternateWeatherPerShift = true;
        [SerializeField] private float transitionDuration = 2.0f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugCycleKey = true;

        public WeatherCondition CurrentCondition { get; private set; } = WeatherCondition.Clear;
        public WeatherPreset CurrentPreset { get; private set; }
        public WeatherStateSnapshot CurrentState { get; private set; }
        public string CurrentBriefingMessage => CurrentPreset != null ? CurrentPreset.BriefingHeadline : string.Empty;
        public int CurrentShiftIndex { get; private set; } = 1;

        public event Action<WeatherCondition, WeatherPreset> OnWeatherChanged;
        public event Action<WeatherStateSnapshot> OnWeatherUpdated;

        private readonly Dictionary<WeatherCondition, WeatherPreset> presetLookup = new Dictionary<WeatherCondition, WeatherPreset>();

        private bool isTransitioning;
        private float transitionTimer;

        private WeatherStateSnapshot startState;
        private WeatherStateSnapshot targetState;

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

            BuildPresetLookup();
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnOrderSelectionStarted += HandleOrderSelectionStarted;
            }

            WeatherCondition initialCondition = randomizeOnStart
                ? GetRandomWeatherCondition()
                : defaultCondition;

            ApplyWeather(initialCondition, instant: true);
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnOrderSelectionStarted -= HandleOrderSelectionStarted;
            }

            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (enableDebugCycleKey && Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame)
            {
                CycleWeather();
            }

            if (!isTransitioning) return;

            transitionTimer += Time.deltaTime;
            float t = transitionDuration > 0.001f
                ? Mathf.Clamp01(transitionTimer / transitionDuration)
                : 1f;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            PublishInterpolatedState(smoothT);

            if (t >= 1f)
            {
                isTransitioning = false;
            }
        }

        public void ApplyWeather(WeatherCondition condition, bool instant = false)
        {
            if (!presetLookup.TryGetValue(condition, out WeatherPreset preset) || preset == null)
            {
                preset = WeatherPreset.CreateRuntimeDefault(condition);
                presetLookup[condition] = preset;
            }

            CurrentCondition = condition;
            CurrentPreset = preset;

            startState = CurrentState;
            targetState = new WeatherStateSnapshot
            {
                Condition = condition,
                FogMode = preset.FogMode,
                EnableFog = preset.EnableFog,
                FogDensity = preset.EnableFog ? preset.FogDensity : 0f,
                FogColor = preset.FogColor,
                RainEmission = preset.EnableRain ? preset.RainEmissionRate : 0f,
                RainSpeed = preset.RainSpeedMultiplier,
                RainTint = preset.RainTint,
                NormalizedRainIntensity = Mathf.Clamp01((preset.EnableRain ? preset.RainEmissionRate : 0f) / 220f),
                WetRoad = preset.WetRoad,
                RoadSmoothness = preset.RoadSmoothness,
                RoadMetallic = preset.RoadMetallic,
                RoadTint = preset.RoadTintMultiplier,
                LightMultiplier = preset.DirectionalLightMultiplier,
                SkyColor = preset.FogColor,
                TransitionProgress = 1f
            };

            if (instant || transitionDuration <= 0.001f)
            {
                isTransitioning = false;
                PublishInterpolatedState(1f);
            }
            else
            {
                transitionTimer = 0f;
                isTransitioning = true;
            }

            Debug.Log($"[WeatherManager] Applied weather: {preset.DisplayName} ({condition}) | FogDensity={targetState.FogDensity:F3} | Rain={targetState.RainEmission:F0} | WetRoad={preset.WetRoad}");
            OnWeatherChanged?.Invoke(CurrentCondition, CurrentPreset);
        }

        public WeatherCondition ApplyBriefingCondition(string briefingText, bool instant = false)
        {
            WeatherCondition parsed = ParseConditionFromBriefing(briefingText);
            ApplyWeather(parsed, instant);
            return parsed;
        }

        public WeatherCondition SelectDailyWeather(int shiftDayIndex, bool instant = false)
        {
            CurrentShiftIndex = Mathf.Max(1, shiftDayIndex);
            WeatherCondition chosen = (WeatherCondition)((CurrentShiftIndex - 1) % 4);
            ApplyWeather(chosen, instant);
            return chosen;
        }

        [ContextMenu("Cycle Weather Preset")]
        public void CycleWeather()
        {
            int next = ((int)CurrentCondition + 1) % 4;
            ApplyWeather((WeatherCondition)next, instant: false);
        }

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

        private void PublishInterpolatedState(float t)
        {
            float rainEmission = Mathf.Lerp(startState.RainEmission, targetState.RainEmission, t);
            CurrentState = new WeatherStateSnapshot
            {
                Condition = targetState.Condition,
                FogMode = targetState.FogMode,
                EnableFog = targetState.EnableFog || startState.EnableFog,
                FogDensity = Mathf.Lerp(startState.FogDensity, targetState.FogDensity, t),
                FogColor = Color.Lerp(startState.FogColor, targetState.FogColor, t),
                RainEmission = rainEmission,
                RainSpeed = Mathf.Lerp(startState.RainSpeed, targetState.RainSpeed, t),
                RainTint = Color.Lerp(startState.RainTint, targetState.RainTint, t),
                NormalizedRainIntensity = Mathf.Clamp01(rainEmission / 220f),
                WetRoad = targetState.WetRoad,
                RoadSmoothness = Mathf.Lerp(startState.RoadSmoothness, targetState.RoadSmoothness, t),
                RoadMetallic = Mathf.Lerp(startState.RoadMetallic, targetState.RoadMetallic, t),
                RoadTint = Color.Lerp(startState.RoadTint, targetState.RoadTint, t),
                LightMultiplier = Mathf.Lerp(startState.LightMultiplier, targetState.LightMultiplier, t),
                SkyColor = Color.Lerp(startState.SkyColor, targetState.SkyColor, t),
                TransitionProgress = t
            };

            OnWeatherUpdated?.Invoke(CurrentState);
        }

        private void HandleOrderSelectionStarted()
        {
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

            foreach (WeatherCondition cond in Enum.GetValues(typeof(WeatherCondition)))
            {
                if (!presetLookup.ContainsKey(cond))
                {
                    presetLookup[cond] = WeatherPreset.CreateRuntimeDefault(cond);
                }
            }
        }

        private static WeatherCondition GetRandomWeatherCondition()
        {
            return (WeatherCondition)UnityEngine.Random.Range(0, 4);
        }
    }
}
