using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Prefab-first authoring; main scenes and GPS assets are never edited.</summary>
public static class DeliverySummarySetup
{
    [InitializeOnLoadMethod]
    private static void RunRequestedBuild()
    {
        if (File.Exists("Temp/DeliverySummaryBuild.request")) EditorApplication.update += BuildWhenReady;
    }
    private static void BuildWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= BuildWhenReady;
        File.Delete("Temp/DeliverySummaryBuild.request");
        try { Build(); File.WriteAllText("Temp/DeliverySummaryBuild.result", "PASS"); }
        catch (Exception error) { File.WriteAllText("Temp/DeliverySummaryBuild.result", error.ToString()); Debug.LogException(error); }
    }

    private const string Path = "Assets/_Game/Prefabs/UI/DeliverySummaryCanvas.prefab";
    private const string CarPath = "Assets/_Game/Prefabs/Player/carInterior.prefab";

    [MenuItem("DoomDriver/Summary/Create missing summary prefab")]
    public static void Build()
    {
        Directory.CreateDirectory("Assets/_Game/Prefabs/UI");
        AssetDatabase.Refresh();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Path) == null) CreatePrefab();
        var car = PrefabUtility.LoadPrefabContents(CarPath);
        try
        {
            if (car.GetComponentInChildren<DeliverySummaryUI>(true) == null)
            {
                var child = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Path), car.transform);
                child.name = "DeliverySummaryCanvas";
                PrefabUtility.SaveAsPrefabAsset(car, CarPath);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(car); }
        CreateTestScene();
        AssetDatabase.SaveAssets();
        Validate();
        Preview();
    }

    [MenuItem("DoomDriver/Summary/Create missing test scene")]
    public static void CreateTestScene()
    {
        const string scenePath = "Assets/_Game/Scenes/_Tests/DeliverySummaryTest.unity";
        if (File.Exists(scenePath)) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Path) == null) CreatePrefab();
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var camera = new GameObject("Summary Camera", typeof(Camera), typeof(AudioListener));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera, scene);
            camera.tag = "MainCamera";
            camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camera.GetComponent<Camera>().backgroundColor = new Color(0.08f, 0.11f, 0.14f);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Path), scene);
            var demo = new GameObject("Summary Demo - Inspector context menu");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(demo, scene);
            var wallet = demo.AddComponent<WalletManager>();
            var fields = new SerializedObject(wallet);
            fields.FindProperty("persistBalance").boolValue = false;
            fields.ApplyModifiedPropertiesWithoutUndo();
            // This UI harness does not drive roads or write distance highscores.
            demo.AddComponent<DeliveryProgressTracker>().enabled = false;
            demo.AddComponent<WorldSpeedController>().enabled = false;
            demo.AddComponent<VehicleHealth>();
            demo.AddComponent<GameManager>();
            demo.AddComponent<DeliverySummaryDemo>();
            if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new Exception("Could not save summary test scene");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    private static void CreatePrefab()
    {
        var root = new GameObject("DeliverySummaryCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        try
        {
            root.layer = 5;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var shade = Box(root.transform, "Backdrop", Vector2.zero, Vector2.zero, new Color(0.025f, 0.04f, 0.055f, 0.8f));
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = Vector2.one;
            shade.GetComponent<Image>().raycastTarget = true;
            var card = Box(root.transform, "Receipt", Vector2.zero, new Vector2(640, 660), new Color(0.1f, 0.14f, 0.18f));
            Text(card, "Heading", -272, 265, 544, 62, 38, "Delivery complete", false);
            var fee = Row(card, "Base fee", 165, "+€15.00");
            var tip = Row(card, "Time tip", 113, "+€0.00");
            var damage = Row(card, "Damage penalties", 61, "−€0.00");
            var missed = Row(card, "Missed-turn penalties", 9, "−€0.00");
            Box(card, "Total divider", new Vector2(0, -43), new Vector2(544, 1), new Color(0.23f, 0.3f, 0.35f));
            Text(card, "Net label", -272, -105, 270, 68, 24, "Net earnings", false);
            var net = Text(card, "Net amount", 0, -105, 272, 68, 48, "€15.00", true);
            net.color = new Color(0.6f, 0.77f, 0.84f);
            var details = Text(card, "Details", -272, -165, 544, 36, 19, string.Empty, false);
            details.gameObject.SetActive(false);
            var buttonRect = Box(card, "Continue", new Vector2(0, -247), new Vector2(544, 64), new Color(0.25f, 0.39f, 0.47f));
            buttonRect.GetComponent<Image>().raycastTarget = true;
            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonRect.GetComponent<Image>();
            var caption = Text(buttonRect, "Caption", -252, 0, 504, 50, 26, "Continue", false);
            caption.alignment = TextAlignmentOptions.Center;
            caption.color = new Color(0.95f, 0.97f, 0.98f);
            var ui = root.AddComponent<DeliverySummaryUI>();
            var so = new SerializedObject(ui);
            Set(so, "panel", root.GetComponent<CanvasGroup>());
            Set(so, "baseFeeText", fee);
            Set(so, "timeTipText", tip);
            Set(so, "damageText", damage);
            Set(so, "missedTurnsText", missed);
            Set(so, "netText", net);
            Set(so, "detailText", details);
            Set(so, "continueButton", button);
            so.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<CanvasGroup>().alpha = 0;
            root.GetComponent<CanvasGroup>().blocksRaycasts = false;
            root.GetComponent<CanvasGroup>().interactable = false;
            PrefabUtility.SaveAsPrefabAsset(root, Path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    private static TMP_Text Row(Transform parent, string title, float y, string amount)
    {
        Text(parent, title + " label", -272, y, 360, 44, 26, title, false);
        return Text(parent, title + " amount", 112, y, 160, 44, 26, amount, true);
    }
    private static RectTransform Box(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        go.GetComponent<Image>().color = color;
        go.GetComponent<Image>().raycastTarget = false;
        return rect;
    }
    private static TMP_Text Text(Transform parent, string name, float x, float y, float width, float height, float size, string value, bool right)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.pivot = new Vector2(0, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        var label = go.GetComponent<TMP_Text>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = size;
        label.text = value;
        label.color = new Color(0.84f, 0.9f, 0.93f);
        label.alignment = right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
        label.raycastTarget = false;
        return label;
    }
    private static void Set(SerializedObject so, string name, UnityEngine.Object value) => so.FindProperty(name).objectReferenceValue = value;

    [MenuItem("DoomDriver/Summary/DEMO complete with accounting values")]
    public static void CompleteDemo()
    {
        if (!Application.isPlaying || GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.DeliveryActive)
        {
            Debug.LogWarning("[Summary demo] Enter Play mode and start a delivery first.");
            return;
        }
        GameManager.Instance.SetDeliveryReward(new DeliveryResult(15m, 3.50m, 2m, 1m));
        GameManager.Instance.CompleteDelivery();
    }

    [MenuItem("DoomDriver/Summary/Validate summary")]
    public static void Validate()
    {
        var r = new DeliveryResult(15m, 3.456m, 2.125m, 1m);
        if (r.NetEarnings != 15.33m || r.TimeTip != 3.46m || r.DamagePenalty != 2.13m) throw new Exception("Currency rounding mismatch");
        if (new DeliveryResult(1, 0, 10, 0).NetEarnings != 0) throw new Exception("Debt floor mismatch");
        bool rejected = false;
        try { new DeliveryResult(-1, 0, 0, 0); } catch (ArgumentOutOfRangeException) { rejected = true; }
        if (!rejected) throw new Exception("Invalid input accepted");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        if (prefab == null) throw new Exception("Missing prefab");
        var so = new SerializedObject(prefab.GetComponent<DeliverySummaryUI>());
        foreach (string field in new[] { "panel", "baseFeeText", "timeTipText", "damageText", "missedTurnsText", "netText", "detailText", "continueButton" })
            if (so.FindProperty(field).objectReferenceValue == null) throw new Exception("Unbound " + field);
        const string testScenePath = "Assets/_Game/Scenes/_Tests/DeliverySummaryTest.unity";
        if (!File.Exists(testScenePath)) throw new Exception("Missing isolated summary test scene");
        var testScene = EditorSceneManager.OpenPreviewScene(testScenePath);
        try
        {
            DeliverySummaryDemo demo = null;
            DeliverySummaryUI testUi = null;
            foreach (var root in testScene.GetRootGameObjects())
            {
                if (root.GetComponent<DeliverySummaryDemo>() != null) demo = root.GetComponent<DeliverySummaryDemo>();
                if (root.GetComponent<DeliverySummaryUI>() != null) testUi = root.GetComponent<DeliverySummaryUI>();
            }
            if (demo == null || testUi == null) throw new Exception("Test scene missing demo or real summary prefab");
            var wallet = demo.GetComponent<WalletManager>();
            if (wallet == null || wallet.PersistsBalance) throw new Exception("Test wallet must not use PlayerPrefs");
            if (demo.GetComponent<GameManager>() == null || demo.GetComponent<VehicleHealth>() == null)
                throw new Exception("Test lifecycle components missing");
            var tracker = demo.GetComponent<DeliveryProgressTracker>();
            if (tracker == null || tracker.enabled) throw new Exception("Test tracker must be disabled to isolate highscore storage");
        }
        finally { EditorSceneManager.ClosePreviewScene(testScene); }
        File.WriteAllText("Temp/DeliverySummaryValidation.txt", "PASS: currency rounding, debt floor, input validation, prefab references, isolated test scene and storage settings.");
        Debug.Log("[Summary] Validation passed.");
    }

    [MenuItem("DoomDriver/Summary/Render preview")]
    public static void Preview()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        RenderTexture rt = null;
        Texture2D image = null;
        var previous = RenderTexture.active;
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Path), scene);
            var cameraGo = new GameObject("Preview camera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGo, scene);
            var camera = cameraGo.GetComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.11f, 0.14f);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            root.GetComponent<CanvasGroup>().alpha = 1;
            rt = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = rt;
            image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            File.WriteAllBytes("Temp/DeliverySummaryPreview.png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
