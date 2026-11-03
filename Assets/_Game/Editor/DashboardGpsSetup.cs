using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Reproducible asset authoring. Never edits the main scene.</summary>
public static class DashboardGpsSetup
{
    private const string PrefabPath = "Assets/_Game/Prefabs/Navigation/DashboardNavigationCanvas.prefab";
    private const string CarPath = "Assets/_Game/Prefabs/Player/carInterior.prefab";
    private const string ScenePath = "Assets/_Game/Scenes/_Tests/DashboardGpsTest.unity";

    [MenuItem("DoomDriver/GPS/Create missing GPS assets")]
    public static void Build()
    {
        Directory.CreateDirectory("Assets/_Game/Prefabs/Navigation");
        CreateBeep();
        AssetDatabase.Refresh();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) CreateDisplay();
        InstallInCar();
        CreateTestScene();
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log("[GPS] Assets ready. Open DashboardGpsTest and press Play.");
    }

    private static void CreateDisplay()
    {
        var root = new GameObject("DashboardNavigationCanvas", typeof(RectTransform), typeof(Canvas));
        try
        {
            root.layer = 5;
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(600, 300);
            rect.localScale = Vector3.one * 0.00075f;
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            Housing(root.transform, "GPS Housing", new Vector3(0, 0, 10), new Vector3(608, 308, 12));
            Box(root.transform, "Background", Vector2.zero, new Vector2(600, 300), new Color(0.09f, 0.095f, 0.09f));
            Box(root.transform, "Accent", Vector2.zero, new Vector2(568, 268), new Color(0.025f, 0.035f, 0.025f));
            var status = Label(root.transform, "Status", new Vector2(65, 98), new Vector2(390, 60), 44, "Waiting for route");
            var distance = Label(root.transform, "Distance", new Vector2(65, -24), new Vector2(390, 170), 120, "--");
            distance.enableAutoSizing = true;
            distance.fontSizeMin = 96;
            distance.fontSizeMax = 120;
            Label(root.transform, "Brand", new Vector2(0, -97), new Vector2(440, 30), 16, "GPS").gameObject.SetActive(false);
            var arrow = new GameObject("ManeuverArrow", typeof(RectTransform));
            arrow.transform.SetParent(root.transform, false);
            var ar = (RectTransform)arrow.transform;
            ar.anchoredPosition = new Vector2(-193, 0);
            ar.localScale = Vector3.one * 1.5f;
            ar.sizeDelta = new Vector2(100, 100);
            var color = new Color(0.87f, 0.9f, 0.82f);
            Box(ar, "Shaft", new Vector2(0, -10), new Vector2(15, 65), color);
            var left = Box(ar, "HeadLeft", new Vector2(-14, 24), new Vector2(15, 48), color);
            left.localRotation = Quaternion.Euler(0, 0, -45);
            var right = Box(ar, "HeadRight", new Vector2(14, 24), new Vector2(15, 48), color);
            right.localRotation = Quaternion.Euler(0, 0, 45);
            var source = root.AddComponent<DashboardNavigationSource>();
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.loop = false;
            audio.spatialBlend = 0f;
            audio.volume = 0.55f;
            var ui = root.AddComponent<DashboardNavigationUI>();
            var serialized = new SerializedObject(ui);
            Assign(serialized, "source", source);
            Assign(serialized, "distanceText", distance);
            Assign(serialized, "statusText", status);
            Assign(serialized, "arrow", ar);
            Assign(serialized, "warningAudio", audio);
            Assign(serialized, "warningClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/gps_beep.wav"));
            Assign(serialized, "turnLeftVoice", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/gps_turn_left.wav"));
            Assign(serialized, "turnRightVoice", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/gps_turn_right.wav"));
            Assign(serialized, "straightVoice", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/gps_straight.wav"));
            Assign(serialized, "recalculatingVoice", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/gps_recalculating.wav"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            arrow.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static RectTransform Box(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        image.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/GpsPanelDepth.mat");
        return rect;
    }

    private static void Housing(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        var renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Black.mat");
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static TMP_Text Label(Transform parent, string name, Vector2 position, Vector2 size, float fontSize, string text)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var label = go.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.87f, 0.9f, 0.82f);
        label.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/GpsTextDepth.mat");
        label.raycastTarget = false;
        label.text = text;
        return label;
    }

    private static void Assign(SerializedObject target, string field, UnityEngine.Object value)
        => target.FindProperty(field).objectReferenceValue = value;

    private static void InstallInCar()
    {
        var car = PrefabUtility.LoadPrefabContents(CarPath);
        try
        {
            if (car.GetComponentInChildren<DashboardNavigationUI>(true) != null) return;
            var display = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), car.transform);
            // Fit into the sloping centre fascia in place of the two centre vents.
            // Grow upward from the previous 260px panel without moving its lower edge.
            float extension = 20f * 0.00075f;
            float tilt = 23.7f * Mathf.Deg2Rad;
            display.transform.localPosition = new Vector3(0.15f,
                0.4463f + extension * Mathf.Cos(tilt), 0.363f - extension * Mathf.Sin(tilt));
            display.transform.localRotation = Quaternion.Euler(23.7f, 180f, 0f);
            car.transform.Find("Vent.001")?.gameObject.SetActive(false);
            car.transform.Find("Vent.002")?.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(car, CarPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(car); }
    }

    private static void CreateTestScene()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var cameraObject = new GameObject("Driver Camera", typeof(Camera), typeof(AudioListener));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0, 1.307f, 0);
            cameraObject.transform.rotation = Quaternion.Euler(-3.65f, 0, 0);
            var camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = 60;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.12f, 0.16f);
            var light = new GameObject("Preview Light", typeof(Light));
            SceneManager.MoveGameObjectToScene(light, scene);
            light.GetComponent<Light>().type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(40, -30, 0);
            var car = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CarPath), scene);
            var ui = car.GetComponentInChildren<DashboardNavigationUI>(true);
            var driver = new GameObject("GPS Demo - Inspector context menu").AddComponent<DashboardNavigationDemo>();
            SceneManager.MoveGameObjectToScene(driver.gameObject, scene);
            var serialized = new SerializedObject(driver);
            Assign(serialized, "display", ui);
            Assign(serialized, "source", ui.Source);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (!File.Exists(ScenePath) && !EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save GPS test scene");
            ui.SetPreviewState(true, false, true);
            ui.Source.Publish("preview", DashboardNavigationSource.Maneuver.Left, 125);
            Canvas.ForceUpdateCanvases();
            var render = new RenderTexture(1280, 720, 24);
            var oldTarget = RenderTexture.active;
            var picture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.scene = scene;
                camera.targetTexture = render;
                camera.Render();
                RenderTexture.active = render;
                picture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                picture.Apply();
                File.WriteAllBytes("Temp/DashboardGpsPreview.png", picture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = oldTarget;
                UnityEngine.Object.DestroyImmediate(picture);
                UnityEngine.Object.DestroyImmediate(render);
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void CreateBeep()
    {
        const string path = "Assets/_Game/Audio/gps_beep.wav";
        if (File.Exists(path)) return;
        const int rate = 44100;
        const int samples = 18522;
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
            for (int i = 0; i < samples; i++)
            {
                double t = (double)i / rate;
                double pulse = t < 0.14 ? t : t >= 0.23 && t < 0.37 ? t - 0.23 : -1;
                double envelope = pulse < 0 ? 0 : Math.Min(1, pulse / 0.008) * Math.Min(1, (0.14 - pulse) / 0.035);
                writer.Write((short)(Math.Sin(2 * Math.PI * 880 * t) * envelope * 11000));
            }
        }
    }

    [MenuItem("DoomDriver/GPS/Validate GPS assets")]
    public static void Validate()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new InvalidOperationException("Missing GPS prefab");
        var ui = prefab.GetComponent<DashboardNavigationUI>();
        var serialized = new SerializedObject(ui);
        foreach (var field in new[] { "source", "distanceText", "statusText", "arrow", "warningAudio", "warningClip",
            "turnLeftVoice", "turnRightVoice", "straightVoice", "recalculatingVoice" })
            if (serialized.FindProperty(field).objectReferenceValue == null) throw new InvalidOperationException("GPS missing " + field);
        if (prefab.GetComponent<Canvas>().renderMode != RenderMode.WorldSpace) throw new InvalidOperationException("GPS must use World Space");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CarPath).GetComponentInChildren<DashboardNavigationUI>(true) == null)
            throw new InvalidOperationException("Car has no GPS");
        var validationScene = EditorSceneManager.NewPreviewScene();
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, validationScene);
        try
        {
            var source = go.GetComponent<DashboardNavigationSource>();
            var display = go.GetComponent<DashboardNavigationUI>();
            var fields = new SerializedObject(display);
            var distance = (TMP_Text)fields.FindProperty("distanceText").objectReferenceValue;
            var status = (TMP_Text)fields.FindProperty("statusText").objectReferenceValue;
            var arrow = (RectTransform)fields.FindProperty("arrow").objectReferenceValue;
            display.SetPreviewState(true, true, true);
            // Edit-mode validation does not run MonoBehaviour.Start subscriptions.
            Action renderSnapshot = () => display.SetPreviewState(true, true);
            source.Changed += renderSnapshot;
            int events = 0;
            source.Changed += () => events++;
            source.Publish("a", DashboardNavigationSource.Maneuver.Left, 72);
            Require(source.RemainingMeters == 72 && events == 1, "snapshot event");
            Require(distance.text == "72 m" && arrow.gameObject.activeSelf, "live distance");
            Require(Mathf.Abs(Mathf.DeltaAngle(arrow.localEulerAngles.z, 90)) < 0.1f, "left arrow");
            source.SetRecalculating();
            Require(source.State == DashboardNavigationSource.NavigationState.Recalculating, "recalculating");
            Require(status.text == "Recalculating..." && !arrow.gameObject.activeSelf && distance.text == "--", "immediate recalculation display");
            source.Publish("b", DashboardNavigationSource.Maneuver.Right, 125);
            Require(Mathf.Abs(Mathf.DeltaAngle(arrow.localEulerAngles.z, -90)) < 0.1f, "right arrow");
            source.Publish("b", DashboardNavigationSource.Maneuver.Straight, 125);
            Require(Mathf.Abs(Mathf.DeltaAngle(arrow.localEulerAngles.z, 0)) < 0.1f, "straight arrow");
            source.Publish("b", DashboardNavigationSource.Maneuver.Right, -5);
            Require(source.RemainingMeters == 0, "negative clamp");
            source.Publish("b", DashboardNavigationSource.Maneuver.Right, float.NaN);
            Require(source.State == DashboardNavigationSource.NavigationState.NoRoute, "invalid snapshot");
            Require(display.GetManeuverClip(DashboardNavigationSource.Maneuver.Left) == fields.FindProperty("turnLeftVoice").objectReferenceValue, "left voice");
            Require(display.GetManeuverClip(DashboardNavigationSource.Maneuver.Right) == fields.FindProperty("turnRightVoice").objectReferenceValue, "right voice");
            Require(display.GetManeuverClip(DashboardNavigationSource.Maneuver.Straight) == fields.FindProperty("straightVoice").objectReferenceValue, "straight voice");
            source.Changed -= renderSnapshot;
            display.SetPreviewState(false, false);
            source.Publish("c", DashboardNavigationSource.Maneuver.Left, 125);
            Require(!arrow.gameObject.activeSelf && status.text == "Waiting for route", "inactive display");
        }
        finally { EditorSceneManager.ClosePreviewScene(validationScene); }
        var gate = new DashboardWarningGate();
        Require(!gate.TryConsume("a", 72, 70, true), "above threshold");
        Require(gate.TryConsume("a", 68, 70, true), "crossing threshold");
        Require(!gate.TryConsume("a", 60, 70, true), "duplicate warning");
        Require(!gate.TryConsume("b", 50, 70, false), "paused/inactive/recalculating");
        Require(gate.TryConsume("b", 50, 70, true), "resume below threshold/new ID");
        Require(!gate.TryConsume("c", 0, 70, true), "passed maneuver");
        Require(!gate.TryConsume("c", float.NaN, 70, true), "invalid distance");
        Require(gate.TryConsume("c", 70, 70, true), "exact threshold");
        Require(!gate.TryConsume("a", 40, 70, true), "old ID after reroute");
        gate.Reset();
        Require(gate.TryConsume("a", 20, 70, true), "new delivery reset");
        Debug.Log("[GPS] Asset and navigation snapshot checks PASSED.");
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("GPS check failed: " + name);
    }
}
