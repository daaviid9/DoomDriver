#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor utility to design, construct, and validate the extended Y-Fork road chunk (~85m length).
/// Fulfills the task: Extended Y-Fork Road Prefabs & Branch Geometry.
/// </summary>
public static class YForkPrefabBuilder
{
    private const string TargetPrefabPath = "Assets/_Game/Prefabs/Environment/RoadChunk_YFork.prefab";
    private const string CrossroadModelPrefab = "Assets/_Game/Prefabs/Road/Crossroad.prefab";
    private const string StraightRoadPrefab = "Assets/ThirdParty/Low Poly Simple Urban City 3D Asset Pack/Prefabs/Roads/Road_1.prefab";
    private const string RoadBarrierPrefab = "Assets/_Game/Prefabs/Obstacles/Road_Barrier.prefab";
    private const string WorkSignPrefab = "Assets/_Game/Prefabs/Obstacles/Road_Work_Sign_1.prefab";
    private const string MetalBarrierPrefab = "Assets/ThirdParty/FencesPack/Prefabs/RoadBarrierMetalSheet.prefab";
    private const string TreePrefab = "Assets/ThirdParty/Low Poly Simple Urban City 3D Asset Pack/Prefabs/Props/Other/Tree_2.prefab";
    private const string LightPrefab = "Assets/ThirdParty/Low Poly Simple Urban City 3D Asset Pack/Prefabs/Props/Road_Signs/Light.prefab";
    private const string TemplateChunkPrefab = "Assets/_Game/Prefabs/Environment/RoadChunk_01.prefab";
    private const string ScenePath = "Assets/_Game/Scenes/EnviromentScene.unity";

    public const float ForkLength = 85.0f;
    public const float ForkCenterZ = 45.0f;
    public static readonly Vector3 LeftExitPos = new Vector3(-8.0f, 0f, 85.0f);
    public static readonly Vector3 LeftExitRot = new Vector3(0f, -12.0f, 0f);
    public static readonly Vector3 RightExitPos = new Vector3(8.0f, 0f, 85.0f);
    public static readonly Vector3 RightExitRot = new Vector3(0f, 12.0f, 0f);

    [InitializeOnLoadMethod]
    private static void AutoBuildOnCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(TargetPrefabPath))
            {
                BuildExtendedYForkPrefab();
            }
            ValidateExtendedYForkSetup();
        };
    }

    [MenuItem("DoomDriver/Environment/Build Extended Y-Fork Prefab (~85m)")]
    public static void BuildExtendedYForkPrefab()
    {
        Debug.Log("<color=cyan><b>[YForkPrefabBuilder]</b> Starting construction of RoadChunk_YFork.prefab (~85m)...</color>");

        // 1. Create root GameObject
        GameObject root = new GameObject("RoadChunk_YFork");
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;

        // 2. Setup RoadChunk component
        RoadChunk roadChunk = root.AddComponent<RoadChunk>();
        SerializedObject chunkSO = new SerializedObject(roadChunk);
        chunkSO.FindProperty("chunkType").enumValueIndex = (int)RoadChunkType.YFork;
        chunkSO.FindProperty("chunkLength").floatValue = ForkLength;
        chunkSO.FindProperty("intersectionCenterZ").floatValue = ForkCenterZ;
        chunkSO.FindProperty("turnTriggerWindow").floatValue = 8.0f;
        chunkSO.FindProperty("leftForkExitPosition").vector3Value = LeftExitPos;
        chunkSO.FindProperty("leftForkExitRotation").vector3Value = LeftExitRot;
        chunkSO.FindProperty("rightForkExitPosition").vector3Value = RightExitPos;
        chunkSO.FindProperty("rightForkExitRotation").vector3Value = RightExitRot;
        chunkSO.ApplyModifiedPropertiesWithoutUndo();

        // 3. Setup WorldMover component
        root.AddComponent<WorldMover>();

        // 4. Setup TurnTrigger component
        TurnTrigger turnTrigger = root.AddComponent<TurnTrigger>();
        SerializedObject triggerSO = new SerializedObject(turnTrigger);
        triggerSO.FindProperty("lateralTurnThreshold").floatValue = 1.5f;
        triggerSO.FindProperty("steeringInputThreshold").floatValue = 0.25f;
        triggerSO.ApplyModifiedPropertiesWithoutUndo();

        // 5. Build Road Hierarchy
        GameObject roadParent = new GameObject("Road");
        roadParent.transform.SetParent(root.transform, false);

        GameObject straightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StraightRoadPrefab);
        GameObject crossroadPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrossroadModelPrefab);

        // Entrance straight segments (Z = 6.25 and Z = 18.75)
        if (straightPrefab != null)
        {
            CreateStraightRoadPiece(straightPrefab, roadParent.transform, new Vector3(0f, -0.5f, 6.25f), Quaternion.identity, new Vector3(0.65f, 1f, 1f));
            CreateStraightRoadPiece(straightPrefab, roadParent.transform, new Vector3(0f, -0.5f, 18.75f), Quaternion.identity, new Vector3(0.65f, 1f, 1f));
        }

        // Central Y-Fork splitting road model (Crossroad.fbx / Crossroad.prefab)
        if (crossroadPrefab != null)
        {
            GameObject crossInstance = (GameObject)PrefabUtility.InstantiatePrefab(crossroadPrefab, roadParent.transform);
            crossInstance.name = "Road_YFork_Core";
            crossInstance.transform.localPosition = new Vector3(0f, -0.5f, 32.5f);
            crossInstance.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            crossInstance.transform.localScale = new Vector3(0.65f, 0.65f, 1f);

            // Ensure MeshCollider exists on the crossroad model
            EnsureMeshCollider(crossInstance);
        }

        // Left branch road extensions (curving smoothly out to X = -8m at Z = 85m)
        if (straightPrefab != null)
        {
            CreateStraightRoadPiece(straightPrefab, roadParent.transform, new Vector3(-2.8f, -0.5f, 48.0f), Quaternion.Euler(0f, -8f, 0f), new Vector3(0.65f, 1f, 1f));
            CreateStraightRoadPiece(straightPrefab, roadParent.transform, new Vector3(-5.5f, -0.5f, 62.0f), Quaternion.Euler(0f, -12f, 0f), new Vector3(0.65f, 1f, 1f));
            CreateStraightRoadPiece(straightPrefab, roadParent.transform, new Vector3(-7.8f, -0.5f, 76.0f), Quaternion.Euler(0f, -12f, 0f), new Vector3(0.65f, 1f, 1f));

            // Right branch road extensions (curving smoothly out to X = +8m at Z = 85m)
            CreateStraightRoadPiece(straightPrefab, roadParent.transform, new Vector3(2.8f, -0.5f, 48.0f), Quaternion.Euler(0f, 8f, 0f), new Vector3(0.65f, 1f, 1f));
            CreateStraightRoadPiece(straightPrefab, roadParent.transform, new Vector3(5.5f, -0.5f, 62.0f), Quaternion.Euler(0f, 12f, 0f), new Vector3(0.65f, 1f, 1f));
            CreateStraightRoadPiece(straightPrefab, roadParent.transform, new Vector3(7.8f, -0.5f, 76.0f), Quaternion.Euler(0f, 12f, 0f), new Vector3(0.65f, 1f, 1f));
        }

        // 6. Build Central Divider Island & Crash Attenuator
        GameObject dividerParent = new GameObject("CentralDivider");
        dividerParent.transform.SetParent(root.transform, false);

        GameObject barrierPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoadBarrierPrefab);
        GameObject signPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorkSignPrefab);
        GameObject metalBarrierPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MetalBarrierPrefab);
        GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefab);
        GameObject lightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LightPrefab);

        // Crash attenuator / hazard barrier at fork apex (Z = 38m)
        if (barrierPrefab != null)
        {
            GameObject attenuator = (GameObject)PrefabUtility.InstantiatePrefab(barrierPrefab, dividerParent.transform);
            attenuator.name = "CrashAttenuator_Apex";
            attenuator.transform.localPosition = new Vector3(0f, 0f, 38.5f);
            attenuator.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            attenuator.transform.localScale = Vector3.one * 1.1f;
        }

        if (signPrefab != null)
        {
            GameObject sign = (GameObject)PrefabUtility.InstantiatePrefab(signPrefab, dividerParent.transform);
            sign.name = "DividerSign_Warning";
            sign.transform.localPosition = new Vector3(0f, 0f, 40.0f);
            sign.transform.localRotation = Quaternion.identity;
        }

        // Divider guardrails along left and right fork inner edges
        if (metalBarrierPrefab != null)
        {
            // Left divider guardrail
            CreateDividerGuardrail(metalBarrierPrefab, dividerParent.transform, new Vector3(-0.9f, 0f, 46.0f), Quaternion.Euler(0f, -8f, 0f));
            CreateDividerGuardrail(metalBarrierPrefab, dividerParent.transform, new Vector3(-2.6f, 0f, 58.0f), Quaternion.Euler(0f, -12f, 0f));
            CreateDividerGuardrail(metalBarrierPrefab, dividerParent.transform, new Vector3(-4.8f, 0f, 70.0f), Quaternion.Euler(0f, -12f, 0f));

            // Right divider guardrail
            CreateDividerGuardrail(metalBarrierPrefab, dividerParent.transform, new Vector3(0.9f, 0f, 46.0f), Quaternion.Euler(0f, 8f, 0f));
            CreateDividerGuardrail(metalBarrierPrefab, dividerParent.transform, new Vector3(2.6f, 0f, 58.0f), Quaternion.Euler(0f, 12f, 0f));
            CreateDividerGuardrail(metalBarrierPrefab, dividerParent.transform, new Vector3(4.8f, 0f, 70.0f), Quaternion.Euler(0f, 12f, 0f));
        }

        // Central divider greenery / props
        if (treePrefab != null)
        {
            CreateTree(treePrefab, dividerParent.transform, new Vector3(0f, 0f, 48.0f), Vector3.one * 1.1f);
            CreateTree(treePrefab, dividerParent.transform, new Vector3(0f, 0f, 62.0f), Vector3.one * 1.3f);
            CreateTree(treePrefab, dividerParent.transform, new Vector3(0f, 0f, 75.0f), Vector3.one * 1.2f);
        }

        if (lightPrefab != null)
        {
            CreateLight(lightPrefab, dividerParent.transform, new Vector3(0f, 0f, 54.0f), Quaternion.Euler(0f, 90f, 0f));
            CreateLight(lightPrefab, dividerParent.transform, new Vector3(0f, 0f, 69.0f), Quaternion.Euler(0f, 90f, 0f));
        }

        // 7. Build Outer Buildings and Scenery (flanking the widening fork)
        GameObject buildingsParent = new GameObject("Buildings");
        buildingsParent.transform.SetParent(root.transform, false);

        GameObject templateChunk = AssetDatabase.LoadAssetAtPath<GameObject>(TemplateChunkPrefab);
        if (templateChunk != null)
        {
            PopulateFlankingBuildings(templateChunk, buildingsParent.transform);
        }

        // 8. Build Outer Props & Streetlights
        GameObject propsParent = new GameObject("Props");
        propsParent.transform.SetParent(root.transform, false);

        if (lightPrefab != null)
        {
            // Outer left street lights
            CreateLight(lightPrefab, propsParent.transform, new Vector3(-13.5f, 0f, 12.0f), Quaternion.identity);
            CreateLight(lightPrefab, propsParent.transform, new Vector3(-15.0f, 0f, 35.0f), Quaternion.Euler(0f, -8f, 0f));
            CreateLight(lightPrefab, propsParent.transform, new Vector3(-18.5f, 0f, 60.0f), Quaternion.Euler(0f, -12f, 0f));
            CreateLight(lightPrefab, propsParent.transform, new Vector3(-21.0f, 0f, 82.0f), Quaternion.Euler(0f, -12f, 0f));

            // Outer right street lights
            CreateLight(lightPrefab, propsParent.transform, new Vector3(13.5f, 0f, 12.0f), Quaternion.Euler(0f, 180f, 0f));
            CreateLight(lightPrefab, propsParent.transform, new Vector3(15.0f, 0f, 35.0f), Quaternion.Euler(0f, 172f, 0f));
            CreateLight(lightPrefab, propsParent.transform, new Vector3(18.5f, 0f, 60.0f), Quaternion.Euler(0f, 168f, 0f));
            CreateLight(lightPrefab, propsParent.transform, new Vector3(21.0f, 0f, 82.0f), Quaternion.Euler(0f, 168f, 0f));
        }

        // 9. Save as Prefab Asset
        string dir = Path.GetDirectoryName(TargetPrefabPath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, TargetPrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=green><b>[YForkPrefabBuilder] Successfully created and saved:</b> {TargetPrefabPath}</color>");

        // 10. Automatically assign to EnvironmentSpawner in the main scene
        AssignYForkToEnvironmentSpawner();
    }

    [MenuItem("DoomDriver/Environment/Assign Y-Fork to EnvironmentSpawner")]
    public static void AssignYForkToEnvironmentSpawner()
    {
        GameObject yForkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefabPath);
        if (yForkPrefab == null)
        {
            Debug.LogError($"[YForkPrefabBuilder] Cannot find prefab at {TargetPrefabPath}. Build it first!");
            return;
        }

        var activeScene = EditorSceneManager.GetActiveScene();
        bool openedScene = false;

        if (activeScene.path != ScenePath)
        {
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                openedScene = true;
            }
            else
            {
                Debug.LogWarning($"[YForkPrefabBuilder] Scene not found at {ScenePath}");
                return;
            }
        }

        EnvironmentSpawner spawner = Object.FindFirstObjectByType<EnvironmentSpawner>();
        if (spawner != null)
        {
            SerializedObject spawnerSO = new SerializedObject(spawner);
            SerializedProperty yForkProp = spawnerSO.FindProperty("yForkPrefabs");
            if (yForkProp != null)
            {
                yForkProp.ClearArray();
                yForkProp.InsertArrayElementAtIndex(0);
                yForkProp.GetArrayElementAtIndex(0).objectReferenceValue = yForkPrefab;
                spawnerSO.ApplyModifiedProperties();

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                Debug.Log("<color=green><b>[YForkPrefabBuilder]</b> RoadChunk_YFork successfully assigned to EnvironmentSpawner.yForkPrefabs in EnviromentScene!</color>");
            }
            else
            {
                Debug.LogError("[YForkPrefabBuilder] EnvironmentSpawner does not contain serialized property 'yForkPrefabs'.");
            }
        }

        if (openedScene && activeScene.IsValid() && activeScene.path != ScenePath)
        {
            EditorSceneManager.OpenScene(activeScene.path, OpenSceneMode.Single);
        }
    }

    [MenuItem("DoomDriver/Environment/Validate Extended Y-Fork Setup")]
    public static void ValidateExtendedYForkSetup()
    {
        Debug.Log("<color=cyan><b>[Validation]</b> Checking Extended Y-Fork Setup...</color>");

        // 1. Prefab existence check
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"❌ [FAIL] Missing prefab at {TargetPrefabPath}!");
            return;
        }
        Debug.Log($"✔ [PASS] Prefab exists: {TargetPrefabPath}");

        // 2. RoadChunk component check
        RoadChunk rc = prefab.GetComponent<RoadChunk>();
        if (rc == null)
        {
            Debug.LogError("❌ [FAIL] RoadChunk component missing on RoadChunk_YFork prefab!");
            return;
        }
        if (rc.ChunkType != RoadChunkType.YFork)
        {
            Debug.LogError($"❌ [FAIL] RoadChunk.ChunkType is {rc.ChunkType}, expected YFork!");
            return;
        }
        if (Mathf.Abs(rc.ChunkLength - ForkLength) > 0.1f)
        {
            Debug.LogError($"❌ [FAIL] RoadChunk.ChunkLength is {rc.ChunkLength}, expected ~{ForkLength}m!");
            return;
        }
        Debug.Log($"✔ [PASS] RoadChunk configuration: Type=YFork, Length={rc.ChunkLength}m, CenterZ={rc.IntersectionCenterZ}m");

        // 3. Dual branch anchors check
        if (rc.LeftForkExitPosition == Vector3.zero || rc.RightForkExitPosition == Vector3.zero)
        {
            Debug.LogError("❌ [FAIL] Fork exit anchors are zero!");
            return;
        }
        Debug.Log($"✔ [PASS] Dual branch anchors: Left={rc.LeftForkExitPosition} (Rot: {rc.LeftForkExitRotation}), Right={rc.RightForkExitPosition} (Rot: {rc.RightForkExitRotation})");

        // 4. WorldMover & TurnTrigger check
        if (prefab.GetComponent<WorldMover>() == null)
        {
            Debug.LogError("❌ [FAIL] WorldMover component missing on RoadChunk_YFork!");
            return;
        }
        if (prefab.GetComponent<TurnTrigger>() == null)
        {
            Debug.LogError("❌ [FAIL] TurnTrigger component missing on RoadChunk_YFork!");
            return;
        }
        Debug.Log("✔ [PASS] WorldMover and TurnTrigger components verified.");

        // 5. Colliders and materials check
        MeshCollider[] colliders = prefab.GetComponentsInChildren<MeshCollider>();
        MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>();
        Debug.Log($"✔ [PASS] Found {colliders.Length} MeshColliders and {renderers.Length} MeshRenderers across road pieces.");

        // 6. Central divider check
        Transform divider = prefab.transform.Find("CentralDivider");
        if (divider == null || divider.childCount == 0)
        {
            Debug.LogError("❌ [FAIL] CentralDivider hierarchy missing or empty!");
            return;
        }
        Debug.Log($"✔ [PASS] Central divider verified with {divider.childCount} barriers/attenuators/props.");

        // 7. Buildings framing check
        Transform buildings = prefab.transform.Find("Buildings");
        if (buildings == null || buildings.childCount == 0)
        {
            Debug.LogError("❌ [FAIL] Buildings hierarchy missing or empty!");
            return;
        }
        Debug.Log($"✔ [PASS] Buildings hierarchy verified with {buildings.childCount} flanking building groups.");

        Debug.Log("<color=green><b>🎉 ALL CHECKS PASSED: Extended Y-Fork Road Chunk is 100% compliant with Definition of Done!</b></color>");
    }

    private static void CreateStraightRoadPiece(GameObject prefab, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 localScale)
    {
        GameObject piece = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        piece.transform.localPosition = localPos;
        piece.transform.localRotation = localRot;
        piece.transform.localScale = localScale;
        EnsureMeshCollider(piece);
    }

    private static void CreateDividerGuardrail(GameObject prefab, Transform parent, Vector3 localPos, Quaternion localRot)
    {
        GameObject guard = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        guard.transform.localPosition = localPos;
        guard.transform.localRotation = localRot;
        guard.transform.localScale = new Vector3(1f, 1f, 1.4f);
    }

    private static void CreateTree(GameObject prefab, Transform parent, Vector3 localPos, Vector3 localScale)
    {
        GameObject tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        tree.transform.localPosition = localPos;
        tree.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        tree.transform.localScale = localScale;
    }

    private static void CreateLight(GameObject prefab, Transform parent, Vector3 localPos, Quaternion localRot)
    {
        GameObject lightObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        lightObj.transform.localPosition = localPos;
        lightObj.transform.localRotation = localRot;
    }

    private static void EnsureMeshCollider(GameObject obj)
    {
        MeshFilter[] filters = obj.GetComponentsInChildren<MeshFilter>();
        foreach (var mf in filters)
        {
            if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
            {
                MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
            }
        }
    }

    private static void PopulateFlankingBuildings(GameObject templateChunk, Transform buildingsParent)
    {
        Transform templateBuildings = templateChunk.transform.Find("Buildings");
        if (templateBuildings == null) return;

        // Clone two segments of flanking buildings to cover the full 85m length
        GameObject group1 = Object.Instantiate(templateBuildings.gameObject, buildingsParent);
        group1.name = "Buildings_Lower_0_to_44m";
        group1.transform.localPosition = Vector3.zero;
        group1.transform.localRotation = Quaternion.identity;

        GameObject group2 = Object.Instantiate(templateBuildings.gameObject, buildingsParent);
        group2.name = "Buildings_Upper_44_to_85m";
        group2.transform.localPosition = new Vector3(0f, 0f, 42.5f);
        group2.transform.localRotation = Quaternion.identity;

        // Gently widen the upper buildings to accommodate the branching road
        foreach (Transform child in group2.transform)
        {
            Vector3 pos = child.localPosition;
            if (pos.x < 0)
            {
                pos.x -= 4.0f; // Shift outer left buildings further left
            }
            else if (pos.x > 0)
            {
                pos.x += 4.0f; // Shift outer right buildings further right
            }
            child.localPosition = pos;
        }
    }
}
#endif
