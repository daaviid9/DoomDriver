using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoomDriver.Environment
{
    /// <summary>
    /// Applies wet/dry road materials and MaterialPropertyBlock reflection properties
    /// to the scene road and spawned RoadChunks based on WeatherManager updates.
    /// </summary>
    public class RoadWetnessReceiver : MonoBehaviour
    {
        [Header("Road Materials & Scene Road")]
        [SerializeField] private Material dryRoadMaterial;
        [SerializeField] private Material wetRoadMaterial;
        [SerializeField] private MeshRenderer sceneRoadRenderer;

        private readonly List<Renderer> trackedRoadRenderers = new List<Renderer>(64);
        private MaterialPropertyBlock roadPropertyBlock;

        private float currentSmoothness = 0.2f;
        private float currentMetallic = 0f;
        private Color currentRoadTint = Color.white;
        private bool lastWetState = false;
        private bool hasAppliedMaterial = false;

        private static readonly int SmoothnessPropId = Shader.PropertyToID("_Smoothness");
        private static readonly int GlossinessPropId = Shader.PropertyToID("_Glossiness");
        private static readonly int MetallicPropId = Shader.PropertyToID("_Metallic");
        private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            roadPropertyBlock = new MaterialPropertyBlock();
            if (sceneRoadRenderer == null)
            {
                GameObject roadObj = GameObject.Find("Road");
                if (roadObj != null) sceneRoadRenderer = roadObj.GetComponent<MeshRenderer>();
            }

            if (dryRoadMaterial == null && sceneRoadRenderer != null)
            {
                dryRoadMaterial = sceneRoadRenderer.sharedMaterial;
            }
        }

        private void Start()
        {
            RefreshExistingChunks();
            Subscribe();

            if (WeatherManager.Instance != null)
            {
                HandleWeatherUpdated(WeatherManager.Instance.CurrentState);
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (EnvironmentSpawner.Instance != null)
            {
                EnvironmentSpawner.Instance.OnChunkSpawned -= RegisterChunkRoadRenderers;
                EnvironmentSpawner.Instance.OnChunkSpawned += RegisterChunkRoadRenderers;
                EnvironmentSpawner.Instance.OnChunkDespawned -= UnregisterChunkRoadRenderers;
                EnvironmentSpawner.Instance.OnChunkDespawned += UnregisterChunkRoadRenderers;
            }

            if (WeatherManager.Instance != null)
            {
                WeatherManager.Instance.OnWeatherUpdated -= HandleWeatherUpdated;
                WeatherManager.Instance.OnWeatherUpdated += HandleWeatherUpdated;
            }
        }

        private void Unsubscribe()
        {
            if (EnvironmentSpawner.Instance != null)
            {
                EnvironmentSpawner.Instance.OnChunkSpawned -= RegisterChunkRoadRenderers;
                EnvironmentSpawner.Instance.OnChunkDespawned -= UnregisterChunkRoadRenderers;
            }

            if (WeatherManager.Instance != null)
            {
                WeatherManager.Instance.OnWeatherUpdated -= HandleWeatherUpdated;
            }
        }

        private void HandleWeatherUpdated(WeatherStateSnapshot state)
        {
            if (sceneRoadRenderer != null && (!hasAppliedMaterial || lastWetState != state.WetRoad))
            {
                lastWetState = state.WetRoad;
                hasAppliedMaterial = true;

                if (state.WetRoad && wetRoadMaterial != null)
                {
                    sceneRoadRenderer.sharedMaterial = wetRoadMaterial;
                }
                else if (!state.WetRoad && dryRoadMaterial != null)
                {
                    sceneRoadRenderer.sharedMaterial = dryRoadMaterial;
                }
            }

            currentSmoothness = state.RoadSmoothness;
            currentMetallic = state.RoadMetallic;
            currentRoadTint = state.RoadTint;

            if (state.WetRoad && wetRoadMaterial != null)
            {
                if (wetRoadMaterial.HasProperty(SmoothnessPropId))
                {
                    currentSmoothness = Mathf.Lerp(currentSmoothness, wetRoadMaterial.GetFloat(SmoothnessPropId), state.TransitionProgress);
                }
                if (wetRoadMaterial.HasProperty(MetallicPropId))
                {
                    currentMetallic = Mathf.Lerp(currentMetallic, wetRoadMaterial.GetFloat(MetallicPropId), state.TransitionProgress);
                }
            }

            ApplyToTrackedRenderers();
        }

        private void ApplyToTrackedRenderers()
        {
            for (int i = trackedRoadRenderers.Count - 1; i >= 0; i--)
            {
                Renderer rend = trackedRoadRenderers[i];
                if (rend == null)
                {
                    trackedRoadRenderers.RemoveAt(i);
                    continue;
                }

                ApplyPropertiesToRenderer(rend);
            }
        }

        private void ApplyPropertiesToRenderer(Renderer rend)
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

                if (roadGroup == null && mr.gameObject.name.IndexOf("Road", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (!trackedRoadRenderers.Contains(mr))
                {
                    trackedRoadRenderers.Add(mr);
                    ApplyPropertiesToRenderer(mr);
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
    }
}

