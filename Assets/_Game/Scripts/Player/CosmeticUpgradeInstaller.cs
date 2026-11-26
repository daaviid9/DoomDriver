using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using DoomDriver.Economy;

/// <summary>
/// Builds the cockpit accessories at scene load and attaches them to the player's car, so cosmetic shop
/// upgrades show up in game without any scene or prefab edits:
/// - a rearview mirror (always; the car model has none) with the Coconut Tree air freshener dangling from it,
/// - a racing-stripes sun strip across the top of the windshield.
/// The hood is not used because the dashboard hides it completely from the driver camera.
/// Positions are world-space measurements of carInterior in EnviromentScene with the Player at the origin.
/// </summary>
public static class CosmeticUpgradeInstaller
{
    private const string FRESHENER_NAME = "Cosmetic_CoconutFreshener";
    private const string STRIPES_NAME = "Cosmetic_RacingStripes";
    private const string MIRROR_NAME = "RearviewMirror";

    // Car centre line and windshield (inner side) measured from carInterior / WindshieldRainGlass.
    private const float CAR_CENTER_X = 0.49f;
    private static readonly Vector2 GlassTopYZ = new Vector2(1.337f, 0.984f);
    private static readonly Vector2 GlassBottomYZ = new Vector2(0.566f, 1.932f);
    private const float GLASS_LEFT_X = -0.87f;
    private const float GLASS_RIGHT_X = 1.845f;

    // Rearview mirror (left-hand drive: driver at x = 0, mirror on the car centre line).
    private static readonly Vector3 MirrorSize = new Vector3(0.24f, 0.07f, 0.025f);
    private const float MIRROR_DROP = 0.075f;   // from the glass top edge to the mirror centre
    private const float MIRROR_INSET = 0.07f;   // towards the driver from the glass
    private const float MIRROR_YAW = 18f;        // turned towards the driver

    // Air freshener (flat cardboard cut-out, like the real ones).
    private const float STRING_LENGTH = 0.05f;
    private const float FRESHENER_HEIGHT = 0.10f;

    // Sun strip with racing stripes.
    private const float STRIP_HEIGHT = 0.11f;    // measured along the glass
    private const float STRIP_INSET = 0.012f;    // just in front of the glass
    private const float STRIPE_HEIGHT = 0.018f;

    private static readonly Color MirrorBody = new Color(0.08f, 0.08f, 0.1f);
    private static readonly Color MirrorGlass = new Color(0.55f, 0.62f, 0.7f);
    private static readonly Color StringColor = new Color(0.9f, 0.88f, 0.8f);
    private static readonly Color StripColor = new Color(0.96f, 0.33f, 0.29f);
    private static readonly Color StripeColor = Color.white;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var vehicle = Object.FindFirstObjectByType<VehicleMovement>();
        if (vehicle != null) Install(vehicle.transform);
    }

    /// <summary>
    /// Adds mirror, freshener and stripes under the vehicle (idempotent). Public so editor tooling can preview it.
    /// </summary>
    public static void Install(Transform vehicle)
    {
        if (vehicle.Find(MIRROR_NAME) != null) return;

        Material template = FindLitMaterial(vehicle);
        if (template == null)
        {
            Debug.LogWarning("[CosmeticUpgradeInstaller] No lit material on the car; cosmetics skipped.");
            return;
        }

        Transform mirror = BuildMirror(vehicle, template);
        BuildFreshener(vehicle, mirror, template);
        BuildRacingStripes(vehicle, template);
    }

    private static Transform BuildMirror(Transform vehicle, Material template)
    {
        Vector3 glassPoint = GlassPointAtY(GlassTopYZ.x - MIRROR_DROP);
        var mirror = new GameObject(MIRROR_NAME).transform;
        mirror.position = new Vector3(CAR_CENTER_X, glassPoint.y, glassPoint.z - MIRROR_INSET);
        mirror.rotation = Quaternion.Euler(4f, -MIRROR_YAW, 0f);

        CreateBox("Body", mirror, Vector3.zero, MirrorSize, Tinted(template, MirrorBody));
        CreateBox("Glass", mirror, new Vector3(0f, 0f, -MirrorSize.z * 0.55f),
            new Vector3(MirrorSize.x * 0.9f, MirrorSize.y * 0.8f, 0.004f), Tinted(template, MirrorGlass));

        // Stem from the mirror up to the windshield top.
        Vector3 top = GlassPointAtY(GlassTopYZ.x - 0.005f);
        Vector3 stemTop = new Vector3(CAR_CENTER_X, top.y, top.z - 0.01f);
        CreateRod("Stem", mirror, mirror.position + mirror.up * (MirrorSize.y * 0.5f), stemTop, 0.012f, Tinted(template, MirrorBody));

        mirror.SetParent(vehicle, true);
        return mirror;
    }

    private static void BuildFreshener(Transform vehicle, Transform mirror, Material template)
    {
        Vector3 hook = mirror.position - mirror.up * (MirrorSize.y * 0.5f) + mirror.right * (MirrorSize.x * 0.25f);
        var root = new GameObject(FRESHENER_NAME).transform;
        root.position = hook;
        root.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

        var visual = new GameObject("Visual").transform; // pivot at the hook, so the dangle swings from it
        visual.SetParent(root, false);

        CreateRod("String", visual, hook, hook + Vector3.down * STRING_LENGTH, 0.002f, Tinted(template, StringColor));

        Sprite palm = LoadIcon(UpgradeType.CoconutFreshener);
        if (palm != null)
        {
            var card = new GameObject("Card").transform;
            card.SetParent(visual, false);
            card.localPosition = Vector3.down * (STRING_LENGTH + FRESHENER_HEIGHT * 0.5f);
            var sprite = card.gameObject.AddComponent<SpriteRenderer>();
            sprite.sprite = palm;
            sprite.color = new Color(0.9f, 0.9f, 0.9f); // unlit sprite: tone down to sit in the darker cabin
            float spriteHeight = palm.bounds.size.y;
            card.localScale = Vector3.one * (FRESHENER_HEIGHT / Mathf.Max(0.0001f, spriteHeight));
        }

        root.SetParent(vehicle, true);
        root.gameObject.AddComponent<CosmeticUpgradeView>().Configure(UpgradeType.CoconutFreshener, visual.gameObject, true);
    }

    private static void BuildRacingStripes(Transform vehicle, Material template)
    {
        Vector3 down = GlassDownDirection();
        Vector3 inward = Vector3.Cross(down, Vector3.right).normalized; // points from the glass towards the driver
        if (inward.z > 0f) inward = -inward;

        Vector3 topEdge = GlassPointAtY(GlassTopYZ.x);
        Vector3 center = topEdge + down * (STRIP_HEIGHT * 0.5f) + inward * STRIP_INSET;
        center.x = (GLASS_LEFT_X + GLASS_RIGHT_X) * 0.5f;
        float width = GLASS_RIGHT_X - GLASS_LEFT_X;

        var root = new GameObject(STRIPES_NAME).transform;
        root.position = center;
        root.rotation = Quaternion.LookRotation(-inward, -down);

        var visual = new GameObject("Visual").transform;
        visual.SetParent(root, false);
        CreateLocalBox("Strip", visual, Vector3.zero, new Vector3(width, STRIP_HEIGHT, 0.004f), Tinted(template, StripColor));
        float offset = STRIPE_HEIGHT * 1.2f;
        CreateLocalBox("StripeUpper", visual, new Vector3(0f, offset, -0.003f), new Vector3(width, STRIPE_HEIGHT, 0.002f), Tinted(template, StripeColor));
        CreateLocalBox("StripeLower", visual, new Vector3(0f, -offset, -0.003f), new Vector3(width, STRIPE_HEIGHT, 0.002f), Tinted(template, StripeColor));

        root.SetParent(vehicle, true);
        root.gameObject.AddComponent<CosmeticUpgradeView>().Configure(UpgradeType.RacingStripes, visual.gameObject, false);
    }

    private static Vector3 GlassPointAtY(float y)
    {
        float t = Mathf.InverseLerp(GlassTopYZ.x, GlassBottomYZ.x, y);
        return new Vector3(CAR_CENTER_X, y, Mathf.Lerp(GlassTopYZ.y, GlassBottomYZ.y, t));
    }

    private static Vector3 GlassDownDirection()
    {
        return new Vector3(0f, GlassBottomYZ.x - GlassTopYZ.x, GlassBottomYZ.y - GlassTopYZ.y).normalized;
    }

    private static Sprite LoadIcon(UpgradeType type)
    {
        var item = Resources.LoadAll<CourierUpgradeItem>("Upgrades").FirstOrDefault(i => i.UpgradeType == type);
        return item != null ? item.Icon : null;
    }

    // Reuse the car's own URP Lit material as a template so the shader is guaranteed to be in builds.
    private static Material FindLitMaterial(Transform vehicle)
    {
        return vehicle.GetComponentsInChildren<MeshRenderer>(true)
            .SelectMany(r => r.sharedMaterials)
            .FirstOrDefault(m => m != null && m.shader != null && m.shader.name == "Universal Render Pipeline/Lit");
    }

    private static Material Tinted(Material template, Color color)
    {
        var material = new Material(template) { name = "Cosmetic_" + ColorUtility.ToHtmlStringRGB(color) };
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        material.color = color;
        return material;
    }

    private static void CreateBox(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        CreateLocalBox(name, parent, localPosition, size, material);
    }

    private static void CreateLocalBox(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        RemoveCollider(box); // must never hit obstacle triggers
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = size;
        box.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void RemoveCollider(GameObject primitive)
    {
        var collider = primitive.GetComponent<Collider>();
        if (Application.isPlaying) Object.Destroy(collider);
        else Object.DestroyImmediate(collider); // editor preview
    }

    private static void CreateRod(string name, Transform parent, Vector3 from, Vector3 to, float thickness, Material material)
    {
        var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rod.name = name;
        RemoveCollider(rod);
        rod.transform.position = (from + to) * 0.5f;
        rod.transform.up = (to - from).normalized;
        rod.transform.localScale = new Vector3(thickness, (to - from).magnitude * 0.5f, thickness);
        rod.transform.SetParent(parent, true);
        rod.GetComponent<MeshRenderer>().sharedMaterial = material;
    }
}
