using UnityEngine;

namespace DoomDriver.Environment
{
    /// <summary>
    /// Applies atmospheric weather visuals: RenderSettings fog, DirectionalLight intensity,
    /// camera background horizon color, and windshield rain particle emission.
    /// </summary>
    public class AtmosphereWeatherReceiver : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private WindshieldRainEffect windshieldRain;
        [SerializeField] private Light directionalLight;
        [SerializeField] private Camera mainCamera;

        private float baseDirectionalIntensity = 1f;
        private Color baseCameraBackgroundColor = new Color(0.1415f, 0.1415f, 0.1415f, 1f);

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            if (WeatherManager.Instance != null)
            {
                WeatherManager.Instance.OnWeatherUpdated -= HandleWeatherUpdated;
                WeatherManager.Instance.OnWeatherUpdated += HandleWeatherUpdated;
                HandleWeatherUpdated(WeatherManager.Instance.CurrentState);
            }
        }

        private void OnDestroy()
        {
            if (WeatherManager.Instance != null)
            {
                WeatherManager.Instance.OnWeatherUpdated -= HandleWeatherUpdated;
            }
        }

        private void HandleWeatherUpdated(WeatherStateSnapshot state)
        {
            RenderSettings.fogMode = state.FogMode;
            RenderSettings.fog = state.FogDensity > 0.0005f;
            RenderSettings.fogDensity = state.FogDensity;
            RenderSettings.fogColor = state.FogColor;

            if (windshieldRain != null)
            {
                windshieldRain.SetRainParameters(state.RainEmission, state.RainSpeed, state.RainTint);
            }

            if (directionalLight != null)
            {
                directionalLight.intensity = baseDirectionalIntensity * state.LightMultiplier;
            }

            if (mainCamera != null && mainCamera.clearFlags == CameraClearFlags.SolidColor)
            {
                mainCamera.backgroundColor = state.EnableFog ? state.SkyColor : baseCameraBackgroundColor;
            }
        }

        private void ResolveReferences()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null)
            {
                baseCameraBackgroundColor = mainCamera.backgroundColor;
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
            }
        }
    }
}

