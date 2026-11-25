using UnityEngine;

namespace DoomDriver.Environment
{
    /// <summary>
    /// Supported weather atmosphere conditions linked to the daily shift briefing.
    /// </summary>
    public enum WeatherCondition
    {
        Clear = 0,
        Foggy = 1,
        LightRain = 2,
        RainAndFog = 3
    }

    /// <summary>
    /// Configurable ScriptableObject preset defining fog density, windshield rain intensity,
    /// wet road reflections, and lighting atmosphere for a given weather condition.
    /// </summary>
    [CreateAssetMenu(fileName = "WeatherPreset", menuName = "DoomDriver/Weather Preset")]
    public class WeatherPreset : ScriptableObject
    {
        [Header("Identity & Daily Briefing")]
        [SerializeField] private WeatherCondition condition = WeatherCondition.Clear;
        [SerializeField] private string displayName = "Clear Skies";
        [TextArea(2, 4)]
        [SerializeField] private string briefingHeadline = "Clear Skies: Visibility is great today. Watch out for rush-hour traffic!";

        [Header("Volumetric Road Fog")]
        [SerializeField] private bool enableFog = false;
        [SerializeField] private FogMode fogMode = FogMode.ExponentialSquared;
        [SerializeField] private Color fogColor = new Color(0.55f, 0.62f, 0.70f, 1f);
        [Range(0f, 0.08f)]
        [SerializeField] private float fogDensity = 0.0f;

        [Header("Windshield Rain Particles")]
        [SerializeField] private bool enableRain = false;
        [Range(0f, 400f)]
        [SerializeField] private float rainEmissionRate = 0f;
        [Range(0.5f, 2.5f)]
        [SerializeField] private float rainSpeedMultiplier = 1f;
        [SerializeField] private Color rainTint = new Color(0.78f, 0.86f, 0.95f, 0.45f);

        [Header("Wet Road Reflection Preset")]
        [SerializeField] private bool wetRoad = false;
        [Range(0f, 1f)]
        [SerializeField] private float roadSmoothness = 0.2f;
        [Range(0f, 1f)]
        [SerializeField] private float roadMetallic = 0.0f;
        [SerializeField] private Color roadTintMultiplier = Color.white;

        [Header("Atmosphere Lighting")]
        [Range(0.3f, 1.2f)]
        [SerializeField] private float directionalLightMultiplier = 1.0f;
        [SerializeField] private Color skyBackgroundTint = new Color(0.14f, 0.14f, 0.14f, 1f);

        public WeatherCondition Condition => condition;
        public string DisplayName => displayName;
        public string BriefingHeadline => briefingHeadline;
        public bool EnableFog => enableFog;
        public FogMode FogMode => fogMode;
        public Color FogColor => fogColor;
        public float FogDensity => fogDensity;
        public bool EnableRain => enableRain;
        public float RainEmissionRate => rainEmissionRate;
        public float RainSpeedMultiplier => rainSpeedMultiplier;
        public Color RainTint => rainTint;
        public bool WetRoad => wetRoad;
        public float RoadSmoothness => roadSmoothness;
        public float RoadMetallic => roadMetallic;
        public Color RoadTintMultiplier => roadTintMultiplier;
        public float DirectionalLightMultiplier => directionalLightMultiplier;
        public Color SkyBackgroundTint => skyBackgroundTint;

        /// <summary>
        /// Configures all preset fields programmatically (used by Editor setup and runtime defaults).
        /// </summary>
        public void Configure(
            WeatherCondition weatherCondition,
            string presetName,
            string headline,
            bool fogEnabled,
            Color fogCol,
            float density,
            bool rainEnabled,
            float emissionRate,
            float speedMult,
            Color rainCol,
            bool isWetRoad,
            float smoothness,
            float metallic,
            Color roadTint,
            float lightMult,
            Color skyBgTint)
        {
            condition = weatherCondition;
            displayName = presetName;
            briefingHeadline = headline;
            enableFog = fogEnabled;
            fogMode = FogMode.ExponentialSquared;
            fogColor = fogCol;
            fogDensity = density;
            enableRain = rainEnabled;
            rainEmissionRate = emissionRate;
            rainSpeedMultiplier = speedMult;
            rainTint = rainCol;
            wetRoad = isWetRoad;
            roadSmoothness = smoothness;
            roadMetallic = metallic;
            roadTintMultiplier = roadTint;
            directionalLightMultiplier = lightMult;
            skyBackgroundTint = skyBgTint;
        }

        /// <summary>
        /// Creates a runtime fallback preset when no ScriptableObject asset is assigned in the Inspector.
        /// </summary>
        public static WeatherPreset CreateRuntimeDefault(WeatherCondition weatherCondition)
        {
            var preset = CreateInstance<WeatherPreset>();
            preset.hideFlags = HideFlags.DontSave;

            switch (weatherCondition)
            {
                case WeatherCondition.Foggy:
                    preset.Configure(
                        WeatherCondition.Foggy,
                        "Morning Fog",
                        "Foggy Morning: Heavy road mist reported across the city. Keep your eyes on the GPS!",
                        fogEnabled: true,
                        fogCol: new Color(0.60f, 0.65f, 0.70f, 1f),
                        density: 0.024f,
                        rainEnabled: false,
                        emissionRate: 0f,
                        speedMult: 1f,
                        rainCol: new Color(0.78f, 0.86f, 0.95f, 0.4f),
                        isWetRoad: false,
                        smoothness: 0.35f,
                        metallic: 0.08f,
                        roadTint: new Color(0.90f, 0.92f, 0.95f, 1f),
                        lightMult: 0.78f,
                        skyBgTint: new Color(0.55f, 0.60f, 0.65f, 1f));
                    break;

                case WeatherCondition.LightRain:
                    preset.Configure(
                        WeatherCondition.LightRain,
                        "Light Rain",
                        "Rainy Shift: Pizza orders are up 40%! Roads are slick and wet—watch those forks!",
                        fogEnabled: true,
                        fogCol: new Color(0.42f, 0.47f, 0.54f, 1f),
                        density: 0.014f,
                        rainEnabled: true,
                        emissionRate: 150f,
                        speedMult: 1.15f,
                        rainCol: new Color(0.75f, 0.85f, 0.96f, 0.45f),
                        isWetRoad: true,
                        smoothness: 0.88f,
                        metallic: 0.32f,
                        roadTint: new Color(0.62f, 0.65f, 0.70f, 1f),
                        lightMult: 0.72f,
                        skyBgTint: new Color(0.35f, 0.40f, 0.47f, 1f));
                    break;

                case WeatherCondition.RainAndFog:
                    preset.Configure(
                        WeatherCondition.RainAndFog,
                        "Rain & Dense Fog",
                        "Stormy Briefing: Rain and thick road fog reducing visibility. Stay alert and don't doomscroll!",
                        fogEnabled: true,
                        fogCol: new Color(0.36f, 0.41f, 0.47f, 1f),
                        density: 0.032f,
                        rainEnabled: true,
                        emissionRate: 260f,
                        speedMult: 1.4f,
                        rainCol: new Color(0.72f, 0.82f, 0.94f, 0.55f),
                        isWetRoad: true,
                        smoothness: 0.92f,
                        metallic: 0.38f,
                        roadTint: new Color(0.55f, 0.58f, 0.64f, 1f),
                        lightMult: 0.60f,
                        skyBgTint: new Color(0.30f, 0.35f, 0.41f, 1f));
                    break;

                case WeatherCondition.Clear:
                default:
                    preset.Configure(
                        WeatherCondition.Clear,
                        "Clear Skies",
                        "Clear Shift: Dry asphalt and clear roads ahead. Great day to rack up speed tips!",
                        fogEnabled: false,
                        fogCol: new Color(0.50f, 0.50f, 0.50f, 1f),
                        density: 0.0f,
                        rainEnabled: false,
                        emissionRate: 0f,
                        speedMult: 1f,
                        rainCol: new Color(0.8f, 0.88f, 0.96f, 0.4f),
                        isWetRoad: false,
                        smoothness: 0.20f,
                        metallic: 0.0f,
                        roadTint: Color.white,
                        lightMult: 1.0f,
                        skyBgTint: new Color(0.1415f, 0.1415f, 0.1415f, 1f));
                    break;
            }

            return preset;
        }
    }
}
