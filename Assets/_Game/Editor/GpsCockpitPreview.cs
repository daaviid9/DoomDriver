using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Read-only cockpit capture for fitting the GPS against the actual loaded scene.</summary>
public static class GpsCockpitPreview
{
    [InitializeOnLoadMethod]
    private static void CaptureRequestedLayout()
    {
        if (File.Exists("Temp/GpsLayoutReview.request")) EditorApplication.update += CaptureWhenReady;
    }

    private static void CaptureWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= CaptureWhenReady;
        File.Delete("Temp/GpsLayoutReview.request");
        try
        {
            CaptureNavigation();
            File.WriteAllText("Temp/GpsLayoutReview.result", "PASS");
        }
        catch (Exception error) { File.WriteAllText("Temp/GpsLayoutReview.result", error.ToString()); }
    }

    [MenuItem("DoomDriver/GPS/Preview active navigation")]
    public static void CaptureNavigation()
    {
        const string gpsPath = "Assets/_Game/Prefabs/Navigation/DashboardNavigationCanvas.prefab";
        const string carPath = "Assets/_Game/Prefabs/Player/carInterior.prefab";
        AssetDatabase.ImportAsset(gpsPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(carPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var original = UnityEngine.Object.FindFirstObjectByType<DashboardNavigationUI>();
        var originalCamera = Camera.main;
        if (original == null || originalCamera == null) throw new InvalidOperationException("Open the cockpit scene first.");
        var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var car = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(carPath), scene);
            car.transform.SetPositionAndRotation(original.transform.parent.position, original.transform.parent.rotation);
            car.transform.localScale = original.transform.parent.lossyScale;
            var cameraObject = UnityEngine.Object.Instantiate(originalCamera.gameObject);
            cameraObject.transform.SetPositionAndRotation(originalCamera.transform.position, originalCamera.transform.rotation);
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            foreach (var light in lights)
            {
                if (!light.isActiveAndEnabled || light.type != LightType.Directional) continue;
                var copy = UnityEngine.Object.Instantiate(light.gameObject);
                copy.transform.SetPositionAndRotation(light.transform.position, light.transform.rotation);
                SceneManager.MoveGameObjectToScene(copy, scene);
            }
            var ui = car.GetComponentInChildren<DashboardNavigationUI>(true);
            ui.Source.Publish("preview-only", DashboardNavigationSource.Maneuver.Left, 125);
            ui.SetPreviewState(true, true);
            Canvas.ForceUpdateCanvases();
            var render = new RenderTexture(1600, 900, 24);
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var oldActive = RenderTexture.active;
            try
            {
                camera.targetTexture = render;
                camera.Render();
                RenderTexture.active = render;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                File.WriteAllBytes("Temp/GpsNavigationPreview.png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = oldActive;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(render);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [MenuItem("DoomDriver/GPS/Capture current cockpit")]
    public static void Capture()
    {
        var camera = Camera.main;
        if (camera == null || !camera.gameObject.scene.IsValid())
            throw new InvalidOperationException("Open the driving scene or DashboardGpsTest first.");
        var report = new StringBuilder();
        report.AppendLine("Scene: " + camera.gameObject.scene.path);
        report.AppendLine("Camera: " + camera.transform.position.ToString("F4") + " Euler: " + camera.transform.eulerAngles.ToString("F3"));
        var display = UnityEngine.Object.FindFirstObjectByType<DashboardNavigationUI>();
        if (display != null)
        {
            Transform car = display.transform.parent;
            report.AppendLine("GPS world: " + display.transform.position.ToString("F4"));
            report.AppendLine("Car: " + car.name + " pos=" + car.position.ToString("F4") + " scale=" + car.lossyScale.ToString("F4"));
            foreach (var renderer in car.GetComponentsInChildren<Renderer>(true))
            {
                report.AppendLine(renderer.name + " | parent=" + renderer.transform.parent.name
                    + " | localPos=" + car.InverseTransformPoint(renderer.transform.position).ToString("F5")
                    + " | worldBounds=" + renderer.bounds.ToString("F4")
                    + " | localBounds=" + renderer.localBounds.ToString("F5"));
            }
            var surfaceScene = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var filter in car.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.name != "CarConstruction") continue;
                    var surface = new GameObject("Temporary fascia measurement");
                    SceneManager.MoveGameObjectToScene(surface, surfaceScene);
                    surface.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                    surface.transform.localScale = filter.transform.lossyScale;
                    var collider = surface.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    Physics.SyncTransforms();
                    foreach (float y in new[] { 0.33f, 0.37f, 0.41f, 0.45f, 0.49f, 0.53f })
                    foreach (float x in new[] { -0.07f, 0.04f, 0.15f, 0.26f, 0.37f })
                    {
                        var ray = new Ray(car.TransformPoint(new Vector3(x, y, 1.5f)), car.TransformDirection(Vector3.back));
                        if (collider.Raycast(ray, out var hit, 6f))
                            report.AppendLine("FASCIA " + x.ToString("F3") + "," + y.ToString("F3") + " => "
                                + car.InverseTransformPoint(hit.point).ToString("F5") + " normal=" + car.InverseTransformDirection(hit.normal).ToString("F4"));
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(surfaceScene); }
        }
        File.WriteAllText("Temp/GpsCockpitGeometry.txt", report.ToString());
        Canvas.ForceUpdateCanvases();
        var render = new RenderTexture(1600, 900, 24);
        var picture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        try
        {
            camera.targetTexture = render;
            camera.Render();
            RenderTexture.active = render;
            picture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            picture.Apply();
            File.WriteAllBytes("Temp/GpsCockpitPreview.png", picture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            UnityEngine.Object.DestroyImmediate(picture);
            UnityEngine.Object.DestroyImmediate(render);
        }
        Debug.Log("[GPS] Saved cockpit capture and geometry report in Temp.");
    }
}
