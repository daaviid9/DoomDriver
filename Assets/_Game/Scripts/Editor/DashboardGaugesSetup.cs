using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor setup helper to locate and wire the speedometer and tachometer needles on carInterior prefab and scene.
/// </summary>
public static class DashboardGaugesSetup
{
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

    private static void EnsureGaugesSetup(GameObject root)
    {
        var gauges = root.GetComponent<DashboardGauges>();
        if (gauges == null) gauges = root.AddComponent<DashboardGauges>();

        var clocks = root.transform.Find("Car/Clocks") ?? root.transform.Find("Clocks");
        if (clocks == null) return;

        // Clock 2 is the Speedometer (dial with 'Clock Backgound Speed')
        var clock2 = clocks.Find("Clock 2");
        Transform speedNeedle = EnsureNeedle(clock2, "SpeedNeedle", new Color(0.95f, 0.15f, 0.1f));

        // Clock 1 is the Tachometer (dial with 'Clock Background Rounds')
        var clock1 = clocks.Find("Clock 1");
        Transform rpmNeedle = EnsureNeedle(clock1, "RpmNeedle", new Color(0.95f, 0.5f, 0.1f));

        // Wire serialized fields on DashboardGauges
        var so = new SerializedObject(gauges);
        var propSpeedNeedle = so.FindProperty("speedometerNeedle");
        var propRpmNeedle = so.FindProperty("tachometerNeedle");

        if (propSpeedNeedle != null && speedNeedle != null) propSpeedNeedle.objectReferenceValue = speedNeedle;
        if (propRpmNeedle != null && rpmNeedle != null) propRpmNeedle.objectReferenceValue = rpmNeedle;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform EnsureNeedle(Transform dialClock, string needleName, Color needleColor)
    {
        if (dialClock == null) return null;

        var existing = dialClock.Find(needleName);
        if (existing != null) return existing;

        // Create needle pivot GameObject exactly centered on the clock face
        // In local space of Clock, normal is (0, 1, 0) and center is (0, 0.015, 0)
        var needlePivot = new GameObject(needleName);
        needlePivot.transform.SetParent(dialClock, false);
        needlePivot.transform.localPosition = new Vector3(0f, 0.016f, 0f);
        needlePivot.transform.localRotation = Quaternion.identity;

        // Create needle pointer geometry (styled thin wedge/tapered pointer)
        var pointer = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pointer.name = "PointerMesh";
        pointer.transform.SetParent(needlePivot.transform, false);

        // Remove collider
        var col = pointer.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        // Position pointer so its tail is at the pivot and its tip extends outward along dial radius
        // Radius of clock face is ~0.07. Pointer length = 0.055, thickness = 0.0025, width = 0.004
        pointer.transform.localPosition = new Vector3(0f, 0.001f, 0.026f);
        pointer.transform.localScale = new Vector3(0.004f, 0.002f, 0.052f);

        // Apply needle material
        var renderer = pointer.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = needleColor;
            renderer.sharedMaterial = mat;
        }

        // Center hub cap (small cylinder)
        var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hub.name = "HubCap";
        hub.transform.SetParent(needlePivot.transform, false);
        var hubCol = hub.GetComponent<Collider>();
        if (hubCol != null) Object.DestroyImmediate(hubCol);

        hub.transform.localPosition = new Vector3(0f, 0.0015f, 0f);
        hub.transform.localScale = new Vector3(0.014f, 0.002f, 0.014f);
        var hubRenderer = hub.GetComponent<MeshRenderer>();
        if (hubRenderer != null)
        {
            var hubMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            hubMat.color = new Color(0.1f, 0.1f, 0.1f);
            hubRenderer.sharedMaterial = hubMat;
        }

        return needlePivot.transform;
    }
}
