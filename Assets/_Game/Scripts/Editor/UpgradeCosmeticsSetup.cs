using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DoomDriver.Economy;

/// <summary>
/// Creates placeholder visuals for the cosmetic shop upgrades in the open scene.
/// They are built from primitives and parented next to the main camera (in the car rig),
/// so they move with the car. Move them onto the real mirror / hood afterwards.
/// </summary>
public static class UpgradeCosmeticsSetup
{
    private const string MATERIAL_FOLDER = "Assets/_Game/Materials/Upgrades";
    private const string COCONUT_NAME = "Cosmetic_CoconutFreshener";
    private const string STRIPES_NAME = "Cosmetic_RacingStripes";

    // Camera-space offsets (right, up, forward) in metres: rearview mirror area and car hood.
    private static readonly Vector3 CoconutCameraOffset = new Vector3(0f, 0.28f, 0.55f);
    private static readonly Vector3 StripesCameraOffset = new Vector3(0f, -0.55f, 1.6f);

    [MenuItem("DoomDriver/Upgrades/Create Cosmetic Placeholders")]
    public static void CreateCosmeticPlaceholders()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            EditorUtility.DisplayDialog("Cosmetic Placeholders", "No camera tagged MainCamera in the open scene.", "OK");
            return;
        }

        Transform rig = camera.transform.parent != null ? camera.transform.parent : camera.transform;
        int created = 0;

        if (GameObject.Find(COCONUT_NAME) == null)
        {
            CreateCoconutFreshener(rig, camera.transform);
            created++;
        }

        if (GameObject.Find(STRIPES_NAME) == null)
        {
            CreateRacingStripes(rig, camera.transform);
            created++;
        }

        EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
        Debug.Log($"[UpgradeCosmeticsSetup] Created {created} cosmetic placeholder(s) under '{rig.name}'. Position them on the mirror / hood and save the scene.");
    }

    private static void CreateCoconutFreshener(Transform rig, Transform camera)
    {
        GameObject root = CreateAnchor(COCONUT_NAME, rig, camera, CoconutCameraOffset, UpgradeType.CoconutFreshener, true);

        // Visual pivot sits at the top of the string so the dangle rotates around the mirror.
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);

        Material stringMat = GetOrCreateMaterial("FreshenerString", new Color(0.9f, 0.9f, 0.85f));
        Material trunkMat = GetOrCreateMaterial("CoconutTrunk", new Color(0.45f, 0.28f, 0.12f));
        Material leafMat = GetOrCreateMaterial("CoconutLeaf", new Color(0.15f, 0.7f, 0.2f));

        CreatePart(PrimitiveType.Cylinder, "String", visual.transform, new Vector3(0f, -0.04f, 0f), Vector3.zero, new Vector3(0.003f, 0.04f, 0.003f), stringMat);
        CreatePart(PrimitiveType.Cube, "Trunk", visual.transform, new Vector3(0f, -0.12f, 0f), new Vector3(0f, 0f, 6f), new Vector3(0.02f, 0.08f, 0.004f), trunkMat);
        CreatePart(PrimitiveType.Sphere, "LeafLeft", visual.transform, new Vector3(-0.025f, -0.075f, 0f), new Vector3(0f, 0f, 25f), new Vector3(0.06f, 0.018f, 0.004f), leafMat);
        CreatePart(PrimitiveType.Sphere, "LeafRight", visual.transform, new Vector3(0.025f, -0.075f, 0f), new Vector3(0f, 0f, -25f), new Vector3(0.06f, 0.018f, 0.004f), leafMat);
        CreatePart(PrimitiveType.Sphere, "LeafTop", visual.transform, new Vector3(0f, -0.068f, 0f), Vector3.zero, new Vector3(0.035f, 0.02f, 0.004f), leafMat);
    }

    private static void CreateRacingStripes(Transform rig, Transform camera)
    {
        GameObject root = CreateAnchor(STRIPES_NAME, rig, camera, StripesCameraOffset, UpgradeType.RacingStripes, false);

        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);

        Material stripeMat = GetOrCreateMaterial("RacingStripe", Color.white);
        CreatePart(PrimitiveType.Cube, "StripeLeft", visual.transform, new Vector3(-0.12f, 0f, 0f), Vector3.zero, new Vector3(0.12f, 0.005f, 1.2f), stripeMat);
        CreatePart(PrimitiveType.Cube, "StripeRight", visual.transform, new Vector3(0.12f, 0f, 0f), Vector3.zero, new Vector3(0.12f, 0.005f, 1.2f), stripeMat);
    }

    private static GameObject CreateAnchor(string name, Transform rig, Transform camera, Vector3 cameraOffset, UpgradeType type, bool dangle)
    {
        var root = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(root, $"Create {name}");
        root.transform.SetParent(rig, false);
        root.transform.position = camera.TransformPoint(cameraOffset);
        root.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized, Vector3.up);

        var view = root.AddComponent<CosmeticUpgradeView>();
        var serialized = new SerializedObject(view);
        serialized.FindProperty("upgradeType").enumValueIndex = (int)type;
        serialized.FindProperty("dangle").boolValue = dangle;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    private static void CreatePart(PrimitiveType primitive, string name, Transform parent, Vector3 position, Vector3 euler, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = name;
        Object.DestroyImmediate(part.GetComponent<Collider>()); // must never hit obstacle triggers
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localEulerAngles = euler;
        part.transform.localScale = scale;
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static Material GetOrCreateMaterial(string name, Color color)
    {
        string path = $"{MATERIAL_FOLDER}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder(MATERIAL_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets/_Game/Materials", "Upgrades");
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
