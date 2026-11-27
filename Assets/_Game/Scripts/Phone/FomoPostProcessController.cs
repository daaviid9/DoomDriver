using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DoomDriver.Phone
{
    /// <summary>
    /// Controls URP Post-Processing (Vignette, ColorAdjustments, DepthOfField)
    /// for lobby focus blur/darkening and driving FOMO tunnel-vision effects.
    /// </summary>
    public class FomoPostProcessController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Volume postProcessVolume;
        [SerializeField] private FomoMeter fomoMeter;

        [Header("Driving FOMO Effects")]
        [SerializeField] private float maxVignetteIntensity = 1f;
        [SerializeField] private float vignettePower = 2f;
        [SerializeField] private float transitionSpeed = 3f;

        [Header("Lobby Presentation")]
        [SerializeField] private bool enableLobbyBlur = true;
        [SerializeField] private float lobbyBlurStart = 0.8f;
        [SerializeField] private float lobbyBlurEnd = 2.2f;
        [SerializeField, Range(0.5f, 2f)] private float lobbyBlurRadius = 1.5f;
        [SerializeField] private bool enableLobbyDarkening = true;
        [SerializeField, Range(0f, 1f)] private float lobbyVignetteIntensity = 0.45f;
        [SerializeField, Range(0.1f, 1f)] private float lobbyVignetteSmoothness = 0.6f;

        private Vignette vignette;
        private ColorAdjustments colorAdjustments;
        private DepthOfField depthOfField;
        private bool isLobbyMode = true;

        private void Awake()
        {
            if (fomoMeter == null) fomoMeter = GetComponent<FomoMeter>();
            ResolveVolume();
        }

        public void SetLobbyMode(bool lobbyMode)
        {
            isLobbyMode = lobbyMode;
        }

        private void ResolveVolume()
        {
            if (postProcessVolume == null) postProcessVolume = GetComponent<Volume>();
            if (postProcessVolume != null && postProcessVolume.profile != null)
            {
                postProcessVolume.profile.TryGet(out vignette);
                postProcessVolume.profile.TryGet(out colorAdjustments);
                postProcessVolume.profile.TryGet(out depthOfField);
            }
        }

        private void Update()
        {
            if (isLobbyMode)
            {
                ApplyLobbyPostProcessing();
            }
            else
            {
                ApplyDrivingPostProcessing();
            }
        }

        private void ApplyLobbyPostProcessing()
        {
            if (depthOfField != null)
            {
                if (enableLobbyBlur)
                {
                    depthOfField.mode.Override(DepthOfFieldMode.Gaussian);
                    depthOfField.gaussianStart.Override(lobbyBlurStart);
                    depthOfField.gaussianEnd.Override(lobbyBlurEnd);
                    depthOfField.gaussianMaxRadius.Override(lobbyBlurRadius);
                }
                else
                {
                    depthOfField.mode.Override(DepthOfFieldMode.Off);
                }
            }

            if (vignette != null)
            {
                float target = enableLobbyDarkening ? lobbyVignetteIntensity : 0f;
                vignette.intensity.Override(Mathf.Lerp(vignette.intensity.value, target, Time.deltaTime * transitionSpeed));
                vignette.smoothness.Override(lobbyVignetteSmoothness);
            }

            if (colorAdjustments != null)
            {
                colorAdjustments.postExposure.Override(0f);
            }
        }

        private void ApplyDrivingPostProcessing()
        {
            if (depthOfField != null && depthOfField.mode.value != DepthOfFieldMode.Off)
            {
                depthOfField.mode.Override(DepthOfFieldMode.Off);
            }

            if (vignette == null) return;

            float normalized = fomoMeter != null ? fomoMeter.NormalizedValue : 1f;
            float intensityFactor = 1f - Mathf.Pow(normalized, vignettePower);

            vignette.intensity.Override(intensityFactor * maxVignetteIntensity);
            vignette.smoothness.Override(Mathf.Lerp(0.2f, 1f, intensityFactor));

            if (colorAdjustments != null)
            {
                colorAdjustments.postExposure.Override(Mathf.Lerp(0f, -10f, Mathf.Pow(intensityFactor, 3f)));
            }
        }
    }
}
