using System.Collections.Generic;
using UnityEngine;

namespace DoomDriver.Environment
{
    /// <summary>
    /// Spawns and positions the delivery drop-off / finish line prefab along the road ahead
    /// so it reaches the player vehicle at the exact moment RemainingDistance hits zero,
    /// and pulses its material alpha/emission via MaterialPropertyBlock.
    /// </summary>
    public class DeliveryFinishLineSpawner : MonoBehaviour
    {
        [Header("Prefab & Placement")]
        [Tooltip("Finish line / delivery zone prefab to spawn.")]
        [SerializeField] private GameObject finishLinePrefab;

        [Tooltip("Distance ahead (in meters) at which the delivery zone becomes visible.")]
        [SerializeField, Min(10f)] private float spawnAheadDistance = 180f;

        [Tooltip("Height above road Y=0 to prevent z-fighting with the asphalt.")]
        [SerializeField] private float roadHeightOffset = 0.06f;

        [Header("Pulse Effect")]
        [SerializeField, Min(0f)] private float pulseSpeed = 3.5f;
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.2f;
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.75f;

        [Header("Debug / Testing")]
        [Tooltip("When checked in Play Mode, spawns the finish line directly in front of the car so you can tune visuals without driving.")]
        [SerializeField] private bool debugPreviewInFrontOfCar = false;
        [SerializeField, Min(1f)] private float debugPreviewDistance = 15f;

        private struct PulsedRenderer
        {
            public Renderer Renderer;
            public Color BaseColor;
            public int ColorPropertyId;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorId = Shader.PropertyToID("_Color");

        private GameObject activeZone;
        private readonly List<PulsedRenderer> pulsedRenderers = new List<PulsedRenderer>();
        private MaterialPropertyBlock propertyBlock;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ClearZone();
        }

        private void LateUpdate()
        {
            if (finishLinePrefab == null) return;

            float carZ = VehicleMovement.Instance != null ? VehicleMovement.Instance.transform.position.z : 0f;

            if (debugPreviewInFrontOfCar)
            {
                EnsureZoneVisible();
                activeZone.transform.position = new Vector3(0f, roadHeightOffset, carZ + debugPreviewDistance);
                UpdatePulse();
                return;
            }

            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.DeliveryActive)
            {
                if (activeZone != null && GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.DeliveryCompleted)
                {
                    ClearZone();
                }
                return;
            }

            if (DeliveryProgressTracker.Instance == null) return;

            float remaining = DeliveryProgressTracker.Instance.RemainingDistance;
            bool turning = EnvironmentSpawner.Instance != null && EnvironmentSpawner.Instance.IsWorldTurnInProgress;

            if (turning || remaining > spawnAheadDistance)
            {
                if (activeZone != null) activeZone.SetActive(false);
                return;
            }

            EnsureZoneVisible();
            activeZone.transform.position = new Vector3(0f, roadHeightOffset, carZ + remaining);
            UpdatePulse();
        }

        private void EnsureZoneVisible()
        {
            if (activeZone == null)
            {
                SpawnZone();
            }

            if (activeZone != null && !activeZone.activeSelf)
            {
                activeZone.SetActive(true);
            }
        }

        private void Subscribe()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.OnDeliveryStarted -= ClearZone;
            GameManager.Instance.OnDeliveryStarted += ClearZone;
            GameManager.Instance.OnOrderSelectionStarted -= ClearZone;
            GameManager.Instance.OnOrderSelectionStarted += ClearZone;
            GameManager.Instance.OnDeliveryFailed -= ClearZone;
            GameManager.Instance.OnDeliveryFailed += ClearZone;
            GameManager.Instance.OnDaySummaryStarted -= ClearZone;
            GameManager.Instance.OnDaySummaryStarted += ClearZone;
        }

        private void Unsubscribe()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.OnDeliveryStarted -= ClearZone;
            GameManager.Instance.OnOrderSelectionStarted -= ClearZone;
            GameManager.Instance.OnDeliveryFailed -= ClearZone;
            GameManager.Instance.OnDaySummaryStarted -= ClearZone;
        }

        private void SpawnZone()
        {
            if (finishLinePrefab == null)
            {
                Debug.LogWarning("<color=yellow>[DeliveryFinishLineSpawner]</color> Finish Line Prefab is not assigned.");
                return;
            }

            activeZone = Instantiate(finishLinePrefab);
            activeZone.name = "DeliveryFinishZone";
            CacheRenderers(activeZone);
        }

        private void CacheRenderers(GameObject root)
        {
            pulsedRenderers.Clear();
            var renderers = root.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                var mat = r.sharedMaterial;
                if (mat == null) continue;

                int propId = mat.HasProperty(BaseColorId) ? BaseColorId
                           : (mat.HasProperty(LegacyColorId) ? LegacyColorId : -1);
                if (propId == -1) continue;

                pulsedRenderers.Add(new PulsedRenderer
                {
                    Renderer = r,
                    BaseColor = mat.GetColor(propId),
                    ColorPropertyId = propId
                });
            }
        }

        private void UpdatePulse()
        {
            if (pulsedRenderers.Count == 0 || pulseSpeed <= 0f) return;

            // Use unscaledTime so pulsing is visible even if game is paused while tweaking
            float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);

            for (int i = 0; i < pulsedRenderers.Count; i++)
            {
                var entry = pulsedRenderers[i];
                if (entry.Renderer == null) continue;

                Color color = entry.BaseColor;
                color.a = alpha;
                entry.Renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(entry.ColorPropertyId, color);
                entry.Renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void ClearZone()
        {
            pulsedRenderers.Clear();
            if (activeZone != null)
            {
                Destroy(activeZone);
                activeZone = null;
            }
        }
    }
}
