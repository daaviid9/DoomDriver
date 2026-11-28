using UnityEngine;

namespace DoomDriver.Environment
{
    /// <summary>
    /// Drives the car's windshield and side-window raindrop glass shader and windshield wipers
    /// based on WeatherManager updates and vehicle forward speed.
    /// </summary>
    public class VehicleWindshieldWeather : MonoBehaviour
    {
        [Header("Car Windshield Glass")]
        [SerializeField] private MeshRenderer carConstructionRenderer;
        [SerializeField] private int carGlassMaterialIndex = 5;
        [SerializeField] private Material windshieldGlassMaterial;
        [SerializeField] private WindshieldWiperController wiperController;

        private Material runtimeGlassMaterial;
        private float currentRainIntensity = 0f;
        private float currentRainSpeed = 1f;
        private float accumulatedGlassFlowTime = 0f;
        private float accumulatedSideDownTime = 0f;
        private float accumulatedSideBackTime = 0f;

        private static readonly int RainIntensityPropId = Shader.PropertyToID("_RainIntensity");
        private static readonly int FlowTimePropId = Shader.PropertyToID("_FlowTime");
        private static readonly int SideDownTimePropId = Shader.PropertyToID("_SideDownTime");
        private static readonly int SideBackTimePropId = Shader.PropertyToID("_SideBackTime");
        private static readonly int SpeedFactorPropId = Shader.PropertyToID("_SpeedFactor");

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            if (WeatherManager.Instance != null)
            {
                WeatherManager.Instance.OnWeatherUpdated += HandleWeatherUpdated;
                HandleWeatherUpdated(WeatherManager.Instance.CurrentState);
            }
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

        private void OnDisable()
        {
            if (WeatherManager.Instance != null)
            {
                WeatherManager.Instance.OnWeatherUpdated -= HandleWeatherUpdated;
            }
        }

        private void Update()
        {
            if (runtimeGlassMaterial == null || currentRainIntensity <= 0.002f) return;

            float speed = WorldSpeedController.Instance != null ? WorldSpeedController.Instance.CurrentSpeed : 0f;
            float speedNorm = Mathf.Clamp01(speed / 26f);

            float frontFlowVelocity = Mathf.Lerp(0.85f, -1.45f, speedNorm) * currentRainSpeed;
            accumulatedGlassFlowTime += Time.deltaTime * frontFlowVelocity;

            accumulatedSideDownTime += Time.deltaTime * Mathf.Lerp(0.85f, 0.25f, speedNorm) * currentRainSpeed;
            accumulatedSideBackTime += Time.deltaTime * Mathf.Lerp(0.25f, 2.20f, speedNorm) * currentRainSpeed;

            runtimeGlassMaterial.SetFloat(FlowTimePropId, accumulatedGlassFlowTime);
            runtimeGlassMaterial.SetFloat(SideDownTimePropId, accumulatedSideDownTime);
            runtimeGlassMaterial.SetFloat(SideBackTimePropId, accumulatedSideBackTime);
            runtimeGlassMaterial.SetFloat(SpeedFactorPropId, speedNorm);
        }

        private void HandleWeatherUpdated(WeatherStateSnapshot state)
        {
            currentRainIntensity = state.NormalizedRainIntensity;
            currentRainSpeed = state.RainSpeed;

            if (runtimeGlassMaterial != null)
            {
                runtimeGlassMaterial.SetFloat(RainIntensityPropId, currentRainIntensity);
            }

            if (wiperController != null)
            {
                wiperController.SetRainIntensity(currentRainIntensity);
            }
        }

        private void ResolveReferences()
        {
            if (carConstructionRenderer == null)
            {
                carConstructionRenderer = GetComponent<MeshRenderer>();
                if (carConstructionRenderer == null)
                {
                    GameObject carObj = GameObject.Find("CarConstruction");
                    if (carObj != null)
                    {
                        carConstructionRenderer = carObj.GetComponent<MeshRenderer>();
                    }
                }
            }

            if (carConstructionRenderer != null)
            {
                Material[] mats = carConstructionRenderer.materials;
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

            if (wiperController == null)
            {
                wiperController = GetComponent<WindshieldWiperController>() ?? FindFirstObjectByType<WindshieldWiperController>();
            }

            if (wiperController != null && runtimeGlassMaterial != null && carConstructionRenderer != null)
            {
                wiperController.BindGlassMaterial(runtimeGlassMaterial, carConstructionRenderer.transform);
            }
        }
    }
}

