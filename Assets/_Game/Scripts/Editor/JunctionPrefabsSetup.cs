#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor utility to author and validate modular junction prefabs for EnvironmentSpawner.
/// Creates TurnLeft, TurnRight, and Crossroads prefabs based on the 4-way crossroad asset.
/// </summary>
public static class JunctionPrefabsSetup
{
    private const string EnvironmentPrefabsDir = "Assets/_Game/Prefabs/Environment";
    private const string BaseRoadChunkPrefab = "Assets/_Game/Prefabs/Environment/RoadChunk_01.prefab";
    private const string CrossroadsModelPrefab = "Assets/ThirdParty/Low Poly Simple Urban City 3D Asset Pack/Prefabs/Roads/Road_Crossroads_1.prefab";
    private const string ScenePath = "Assets/_Game/Scenes/EnviromentScene.unity";

    [MenuItem("DoomDriver/Environment/Measure Selected Object Bounds")]
    [MenuItem("GameObject/DoomDriver/Measure Bounds", false, 0)]
    public static void MeasureSelectedBounds()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("[MeasureBounds] Najprv označ GameObject alebo Prefab v Hierarchy/Project okne!");
            return;
        }

        Renderer[] renderers = selected.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning($"[MeasureBounds] Objekt '{selected.name}' neobsahuje žiadne Renderery v deťoch.");
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Debug.Log($"<color=cyan><b>[Rozmery chunku '{selected.name}']</b></color>\n" +
                  $"▶ <b>Šírka (os X):</b> {bounds.size.x:F2} m  (od {bounds.min.x:F2} do {bounds.max.x:F2})\n" +
                  $"▶ <b>Dĺžka (os Z):</b> {bounds.size.z:F2} m  (od {bounds.min.z:F2} do {bounds.max.z:F2})\n" +
                  $"▶ <b>Výška (os Y):</b> {bounds.size.y:F2} m");
    }

    [MenuItem("DoomDriver/Environment/Create Missing Junction Prefabs")]
    public static void CreateJunctionPrefabs()
    {
        Directory.CreateDirectory(EnvironmentPrefabsDir);

        GameObject baseChunk = AssetDatabase.LoadAssetAtPath<GameObject>(BaseRoadChunkPrefab);
        if (baseChunk == null)
        {
            Debug.LogError($"[JunctionPrefabsSetup] Could not load base prefab at {BaseRoadChunkPrefab}");
            return;
        }

        // Ensure straight chunks have RoadChunk component
        EnsureRoadChunkOnPrefab("Assets/_Game/Prefabs/Environment/RoadChunk_01.prefab", RoadChunkType.Straight);
        EnsureRoadChunkOnPrefab("Assets/_Game/Prefabs/Environment/RoadChunk_02.prefab", RoadChunkType.Straight);
        EnsureRoadChunkOnPrefab("Assets/_Game/Prefabs/Environment/RoadChunk_03.prefab", RoadChunkType.Straight);

        // Create 3 standardized Junction Prefabs using the 4-way crossroad
        CreateJunctionVariant(baseChunk, "RoadChunk_TurnLeft.prefab", RoadChunkType.TurnLeft);
        CreateJunctionVariant(baseChunk, "RoadChunk_TurnRight.prefab", RoadChunkType.TurnRight);
        CreateJunctionVariant(baseChunk, "RoadChunk_Crossroads.prefab", RoadChunkType.Crossroads);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Assign to EnvironmentSpawner in EnviromentScene
        AssignPrefabsToSpawnerInScene();

        Debug.Log("<color=green>[JunctionPrefabsSetup] Successfully authored junction prefabs and configured EnvironmentSpawner!</color>");
    }

    private static void EnsureRoadChunkOnPrefab(string path, RoadChunkType type)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return;

        bool modified = false;
        if (!prefab.TryGetComponent<RoadChunk>(out var roadChunk))
        {
            roadChunk = prefab.AddComponent<RoadChunk>();
            modified = true;
        }

        var serializedObj = new SerializedObject(roadChunk);
        serializedObj.FindProperty("chunkType").enumValueIndex = (int)type;
        serializedObj.FindProperty("chunkLength").floatValue = 44.75f;
        serializedObj.ApplyModifiedPropertiesWithoutUndo();

        if (modified)
        {
            PrefabUtility.SavePrefabAsset(prefab);
        }
    }

    private static void CreateJunctionVariant(GameObject baseTemplate, string fileName, RoadChunkType type)
    {
        string targetPath = Path.Combine(EnvironmentPrefabsDir, fileName).Replace("\\", "/");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(targetPath) != null)
        {
            Debug.Log($"[JunctionPrefabsSetup] {fileName} already exists. Updating RoadChunk component.");
            EnsureRoadChunkOnPrefab(targetPath, type);
            return;
        }

        // Instantiate clone in memory
        GameObject instance = Object.Instantiate(baseTemplate);
        instance.name = Path.GetFileNameWithoutExtension(fileName);

        // Ensure RoadChunk component
        RoadChunk roadChunk = instance.GetComponent<RoadChunk>();
        if (roadChunk == null) roadChunk = instance.AddComponent<RoadChunk>();

        var serializedObj = new SerializedObject(roadChunk);
        serializedObj.FindProperty("chunkType").enumValueIndex = (int)type;
        serializedObj.FindProperty("chunkLength").floatValue = 44.75f;
        serializedObj.ApplyModifiedPropertiesWithoutUndo();

        // Ensure WorldMover component
        if (instance.GetComponent<WorldMover>() == null)
        {
            instance.AddComponent<WorldMover>();
        }

        // Swap middle road segment with crossroads mesh using exact city scale
        GameObject crossroadsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrossroadsModelPrefab);
        if (crossroadsPrefab != null)
        {
            Transform roadParent = instance.transform.Find("Road");
            if (roadParent != null && roadParent.childCount >= 3)
            {
                Transform middleChild = roadParent.GetChild(2);
                Vector3 localPos = middleChild.localPosition;
                Object.DestroyImmediate(middleChild.gameObject);

                GameObject crInstance = (GameObject)PrefabUtility.InstantiatePrefab(crossroadsPrefab, roadParent);
                crInstance.transform.localPosition = new Vector3(0f, -0.5f, localPos.z);
                crInstance.transform.localRotation = Quaternion.identity;
                crInstance.transform.localScale = new Vector3(0.65f, 1f, 0.811f);
                crInstance.transform.SetSiblingIndex(2);
            }
        }

        // Save as new prefab
        PrefabUtility.SaveAsPrefabAsset(instance, targetPath);
        Object.DestroyImmediate(instance);

        Debug.Log($"[JunctionPrefabsSetup] Created prefab: {targetPath}");
    }

    private static void AssignPrefabsToSpawnerInScene()
    {
        var currentScene = EditorSceneManager.GetActiveScene();
        bool openedScene = false;

        if (currentScene.path != ScenePath)
        {
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                openedScene = true;
            }
            else
            {
                return;
            }
        }

        EnvironmentSpawner spawner = Object.FindFirstObjectByType<EnvironmentSpawner>();
        if (spawner != null)
        {
            SerializedObject spawnerObj = new SerializedObject(spawner);

            // Assign Straight
            AssignArrayProperty(spawnerObj.FindProperty("straightPrefabs"), new[]
            {
                "Assets/_Game/Prefabs/Environment/RoadChunk_01.prefab",
                "Assets/_Game/Prefabs/Environment/RoadChunk_02.prefab",
                "Assets/_Game/Prefabs/Environment/RoadChunk_03.prefab"
            });

            // Assign TurnLeft
            AssignArrayProperty(spawnerObj.FindProperty("turnLeftPrefabs"), new[]
            {
                "Assets/_Game/Prefabs/Environment/RoadChunk_TurnLeft.prefab"
            });

            // Assign TurnRight
            AssignArrayProperty(spawnerObj.FindProperty("turnRightPrefabs"), new[]
            {
                "Assets/_Game/Prefabs/Environment/RoadChunk_TurnRight.prefab"
            });

            // Assign Crossroads
            AssignArrayProperty(spawnerObj.FindProperty("crossroadsPrefabs"), new[]
            {
                "Assets/_Game/Prefabs/Environment/RoadChunk_Crossroads.prefab"
            });

            spawnerObj.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[JunctionPrefabsSetup] Assigned junction prefabs to EnvironmentSpawner in EnviromentScene.");
        }

        if (openedScene && currentScene.IsValid() && currentScene.path != ScenePath)
        {
            EditorSceneManager.OpenScene(currentScene.path, OpenSceneMode.Single);
        }
    }

    private static void AssignArrayProperty(SerializedProperty arrayProp, string[] assetPaths)
    {
        if (arrayProp == null) return;
        arrayProp.ClearArray();
        for (int i = 0; i < assetPaths.Length; i++)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPaths[i]);
            if (asset != null)
            {
                arrayProp.InsertArrayElementAtIndex(i);
                arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = asset;
            }
        }
    }
}
#endif
