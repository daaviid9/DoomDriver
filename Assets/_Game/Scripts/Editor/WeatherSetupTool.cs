#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DoomDriver.Environment;

namespace DoomDriver.Editor
{
    /// <summary>
    /// Editor utility to generate weather ScriptableObject presets, wet road reflection material,
    /// windshield rain prefab, and wire WeatherManager & RainParticles into EnviromentScene.
    /// </summary>
    public static class WeatherSetupTool
    {
        private const string WeatherDataFolder = "Assets/_Game/Data/Weather";
        private const string MaterialsFolder = "Assets/_Game/Materials";
        private const string PrefabsFolder = "Assets/_Game/Prefabs/Environment";

        [MenuItem("DoomDriver/Setup Dynamic Weather")]
        public static void SetupDynamicWeather()
        {
            EnsureFolder(WeatherDataFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);

            Material wetRoadMat = CreateOrUpdateWetRoadMaterial();
            Material rainMat = CreateOrUpdateRainMaterial();
            GameObject rainPrefab = CreateOrUpdateRainPrefab(rainMat);

            WeatherPreset[] presets = new WeatherPreset[]
            {
                CreateOrUpdatePreset(WeatherCondition.Clear, "Weather_Clear.asset"),
                CreateOrUpdatePreset(WeatherCondition.Foggy, "Weather_Foggy.asset"),
                CreateOrUpdatePreset(WeatherCondition.LightRain, "Weather_LightRain.asset"),
                CreateOrUpdatePreset(WeatherCondition.RainAndFog, "Weather_RainAndFog.asset")
            };

            WireActiveScene(presets, wetRoadMat, rainPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[WeatherSetupTool] Dynamic Weather assets and scene wiring completed.");
        }

        private static Material CreateOrUpdateWetRoadMaterial()
        {
            string path = $"{MaterialsFolder}/Road_Wet.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(urpLit != null ? urpLit : Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", new Color(0.36f, 0.38f, 0.42f, 1f));
            mat.SetFloat("_Smoothness", 0.88f);
            mat.SetFloat("_Metallic", 0.32f);
            mat.SetFloat("_EnvironmentReflections", 1f);
            mat.SetFloat("_SpecularHighlights", 1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateOrUpdateRainMaterial()
        {
            string path = $"{MaterialsFolder}/RainStreak.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader urpParticles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                mat = new Material(urpParticles != null ? urpParticles : Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetColor("_BaseColor", new Color(0.78f, 0.86f, 0.96f, 0.48f));
            mat.renderQueue = 3000;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject CreateOrUpdateRainPrefab(Material rainMat)
        {
            string path = $"{PrefabsFolder}/RainWindshield.prefab";
            GameObject tempObj = new GameObject("RainWindshield");
            var effect = tempObj.AddComponent<WindshieldRainEffect>();

            SerializedObject so = new SerializedObject(effect);
            so.FindProperty("rainMaterial").objectReferenceValue = rainMat;
            so.ApplyModifiedPropertiesWithoutUndo();

            effect.InitializeIfNeeded();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tempObj, path);
            Object.DestroyImmediate(tempObj);
            return prefab;
        }

        private static WeatherPreset CreateOrUpdatePreset(WeatherCondition condition, string fileName)
        {
            string path = $"{WeatherDataFolder}/{fileName}";
            WeatherPreset preset = AssetDatabase.LoadAssetAtPath<WeatherPreset>(path);
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<WeatherPreset>();
                AssetDatabase.CreateAsset(preset, path);
            }

            WeatherPreset defaults = WeatherPreset.CreateRuntimeDefault(condition);
            preset.Configure(
                defaults.Condition,
                defaults.DisplayName,
                defaults.BriefingHeadline,
                defaults.EnableFog,
                defaults.FogColor,
                defaults.FogDensity,
                defaults.EnableRain,
                defaults.RainEmissionRate,
                defaults.RainSpeedMultiplier,
                defaults.RainTint,
                defaults.WetRoad,
                defaults.RoadSmoothness,
                defaults.RoadMetallic,
                defaults.RoadTintMultiplier,
                defaults.DirectionalLightMultiplier,
                defaults.SkyBackgroundTint);

            Object.DestroyImmediate(defaults);
            EditorUtility.SetDirty(preset);
            return preset;
        }

        private static void WireActiveScene(WeatherPreset[] presets, Material wetRoadMat, GameObject rainPrefab)
        {
            GameObject managers = GameObject.Find("GameManagers");
            if (managers == null)
            {
                Debug.LogWarning("[WeatherSetupTool] GameManagers not found in active scene.");
                return;
            }

            WeatherManager weatherManager = managers.GetComponent<WeatherManager>();
            if (weatherManager == null)
            {
                weatherManager = managers.AddComponent<WeatherManager>();
            }

            Camera mainCam = Camera.main;
            WindshieldRainEffect rainEffect = null;
            if (mainCam != null)
            {
                rainEffect = mainCam.GetComponentInChildren<WindshieldRainEffect>(true);
                if (rainEffect == null && rainPrefab != null)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(rainPrefab, mainCam.transform);
                    rainEffect = instance.GetComponent<WindshieldRainEffect>();
                }
            }

            Material groundMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Ground.mat");

            SerializedObject so = new SerializedObject(weatherManager);
            SerializedProperty presetsProp = so.FindProperty("presets");
            presetsProp.arraySize = presets.Length;
            for (int i = 0; i < presets.Length; i++)
            {
                presetsProp.GetArrayElementAtIndex(i).objectReferenceValue = presets[i];
            }

            so.FindProperty("windshieldRain").objectReferenceValue = rainEffect;
            so.FindProperty("mainCamera").objectReferenceValue = mainCam;
            so.FindProperty("dryRoadMaterial").objectReferenceValue = groundMat;
            so.FindProperty("wetRoadMaterial").objectReferenceValue = wetRoadMat;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            string name = Path.GetFileName(folderPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
