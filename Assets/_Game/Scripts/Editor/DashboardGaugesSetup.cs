using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor setup helper to locate and wire the speedometer and tachometer needles on carInterior prefab and scene.
/// Also provides single-click test scene generation.
/// </summary>
public static class DashboardGaugesSetup
{
    public const string TestScenePath = "Assets/_Game/Scenes/_Tests/DashboardGaugesTest.unity";
    public const string OrangeMatPath = "Assets/_Game/Materials/GaugeNeedleOrange.mat";
    public const string BlackMatPath = "Assets/_Game/Materials/Black.mat";

    [MenuItem("DoomDriver/Cockpit/Setup Dashboard Gauges")]
    public static void SetupGauges()
    {
        // 1. Process carInterior.prefab
        string prefabPath = "Assets/_Game/Prefabs/Player/carInterior.prefab";
        var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabRoot != null)
        {
            try
            {
                EnsureGaugesSetup(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                Debug.Log("<color=green>[DashboardGaugesSetup]</color> Successfully configured DashboardGauges on " + prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        // 2. Process active scene if open
        var player = GameObject.Find("Player") ?? GameObject.Find("carInterior");
        if (player != null)
        {
            EnsureGaugesSetup(player);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(player.scene);
            Debug.Log("<color=green>[DashboardGaugesSetup]</color> Configured DashboardGauges in open scene on " + player.name);
        }
    }

    [MenuItem("DoomDriver/Cockpit/Create Gauges Test Scene")]
    public static void CreateTestScene()
    {
        System.IO.Directory.CreateDirectory("Assets/_Game/Scenes/_Tests");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, 
            UnityEditor.SceneManagement.NewSceneMode.Single
        );

        try
        {
            // 1. Lighting
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.8f;
            light.color = Color.white;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 2. GameManagers
            var mgrObj = new GameObject("GameManagers");
            mgrObj.AddComponent<GameManager>();
            mgrObj.AddComponent<WorldSpeedController>();
            mgrObj.AddComponent<PauseController>();

            // 3. Car Interior Prefab
            var carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player/carInterior.prefab");
            GameObject car = null;
            if (carPrefab != null)
            {
                car = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab);
                car.name = "Player";
                car.transform.position = Vector3.zero;
                car.transform.rotation = Quaternion.identity;
            }

            // 4. Main Camera positioned for direct cockpit gauge inspection
            var camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            if (car != null) camObj.transform.SetParent(car.transform, false);

            // Cockpit driver eye position focused on the steering wheel & instrument cluster
            camObj.transform.localPosition = new Vector3(0.0f, 1.307f, 0.0f);
            camObj.transform.localRotation = Quaternion.Euler(-3.65f, 0.0f, 0.0f);

            var cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Color;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;
            cam.fieldOfView = 50f;
            camObj.AddComponent<AudioListener>();

            var urpCamType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (urpCamType != null) camObj.AddComponent(urpCamType);

            // Configure Gauges in Test Mode (so they animate immediately without driving)
            if (car != null)
            {
                var gauges = car.GetComponent<DashboardGauges>();
                if (gauges != null)
                {
                    gauges.SetManualTest(true, 85f, 3800f);
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, TestScenePath);
            Debug.Log($"<color=green>[DashboardGaugesSetup]</color> Created gauge test scene at: {TestScenePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    public static void EnsureGaugesSetup(GameObject root)
    {
        var gauges = root.GetComponent<DashboardGauges>();
        if (gauges == null) gauges = root.AddComponent<DashboardGauges>();

        var clocks = root.transform.Find("Car/Clocks") ?? root.transform.Find("Clocks");
        if (clocks == null) return;

        // Load durable project materials
        var orangeMat = AssetDatabase.LoadAssetAtPath<Material>(OrangeMatPath);
        var blackMat = AssetDatabase.LoadAssetAtPath<Material>(BlackMatPath);

        // Clock 2 is the Speedometer (dial with 'Clock Backgound Speed')
        var clock2 = clocks.Find("Clock 2");
        Transform speedNeedle = EnsureNeedle(clock2, "SpeedNeedle", orangeMat, blackMat);

        // Clock 1 is the Tachometer (dial with 'Clock Background Rounds')
        var clock1 = clocks.Find("Clock 1");
        Transform rpmNeedle = EnsureNeedle(clock1, "RpmNeedle", orangeMat, blackMat);

        // Wire serialized fields on DashboardGauges
        var so = new SerializedObject(gauges);
        var propSpeedNeedle = so.FindProperty("speedometerNeedle");
        var propRpmNeedle = so.FindProperty("tachometerNeedle");
        var propSpeedAxis = so.FindProperty("speedRotationAxis");
        var propRpmAxis = so.FindProperty("rpmRotationAxis");

        if (propSpeedNeedle != null && speedNeedle != null) propSpeedNeedle.objectReferenceValue = speedNeedle;
        if (propRpmNeedle != null && rpmNeedle != null) propRpmNeedle.objectReferenceValue = rpmNeedle;

        // Dial face normal in local space is (0, 1, 0)
        if (propSpeedAxis != null) propSpeedAxis.vector3Value = new Vector3(0f, 1f, 0f);
        if (propRpmAxis != null) propRpmAxis.vector3Value = new Vector3(0f, 1f, 0f);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform EnsureNeedle(Transform dialClock, string needleName, Material needleMat, Material hubMat)
    {
        if (dialClock == null) return null;

        var existing = dialClock.Find(needleName);
        if (existing != null)
        {
            // Update materials and local geometry if existing
            UpdateNeedleGeometryAndMaterials(existing, needleMat, hubMat);
            return existing;
        }

        // Needle pivot: centered on dial face. Local Y = 0.015 is the dial face plane
        var needlePivot = new GameObject(needleName);
        needlePivot.transform.SetParent(dialClock, false);
        needlePivot.transform.localPosition = new Vector3(0f, 0.015f, 0f);
        needlePivot.transform.localRotation = Quaternion.identity;

        UpdateNeedleGeometryAndMaterials(needlePivot.transform, needleMat, hubMat);
        return needlePivot.transform;
    }

    private static void UpdateNeedleGeometryAndMaterials(Transform needlePivot, Material needleMat, Material hubMat)
    {
        needlePivot.localPosition = new Vector3(0f, 0.015f, 0f);

        // 1. Pointer Mesh
        var pointer = needlePivot.Find("PointerMesh");
        if (pointer == null)
        {
            var pObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pObj.name = "PointerMesh";
            pointer = pObj.transform;
            pointer.SetParent(needlePivot, false);
            var col = pObj.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        // The needle points outward along the local Z axis of the dial disc
        // Length = 0.05 (fits comfortably within 0.07 radius dial)
        // Thickness along Y = 0.001 (flush against dial surface)
        // Width along X = 0.003
        pointer.localPosition = new Vector3(0f, 0.001f, 0.025f);
        pointer.localScale = new Vector3(0.0035f, 0.001f, 0.048f);
        pointer.localRotation = Quaternion.identity;

        var pointerRend = pointer.GetComponent<MeshRenderer>();
        if (pointerRend != null && needleMat != null)
        {
            pointerRend.sharedMaterial = needleMat;
        }

        // 2. Hub Cap
        var hub = needlePivot.Find("HubCap");
        if (hub == null)
        {
            var hObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hObj.name = "HubCap";
            hub = hObj.transform;
            hub.SetParent(needlePivot, false);
            var col = hObj.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        hub.localPosition = new Vector3(0f, 0.0015f, 0f);
        hub.localScale = new Vector3(0.014f, 0.0012f, 0.014f);
        hub.localRotation = Quaternion.identity;

        var hubRend = hub.GetComponent<MeshRenderer>();
        if (hubRend != null && hubMat != null)
        {
            hubRend.sharedMaterial = hubMat;
        }
    }
}
