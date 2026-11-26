using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DoomDriver.Phone;

namespace DoomDriver.UI
{
    /// <summary>
    /// Attaches the shift overlays (ShiftBriefingUI + ShiftSummaryUI) to the phone OS canvas of every loaded
    /// scene, so no scene or PhoneOSCanvas prefab edit is needed. Runs after Awake and before Start.
    /// shortcut: overlays are built in code instead of a ShiftReportCanvas prefab; move to a prefab
    /// once an artist needs to restyle them in the editor.
    /// </summary>
    public static class ShiftReportInstaller
    {
        private const string ROOT_NAME = "ShiftReportCanvas";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var phoneOS = PhoneOSController.Instance != null
                ? PhoneOSController.Instance
                : Object.FindFirstObjectByType<PhoneOSController>();
            if (phoneOS == null) return; // menu / test scenes without the phone
            if (phoneOS.GetComponentInChildren<ShiftBriefingUI>(true) != null) return;

            var root = new GameObject(ROOT_NAME, typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.SetParent(phoneOS.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();
            root.AddComponent<LayoutElement>().ignoreLayout = true;

            root.AddComponent<ShiftSummaryUI>();
            root.AddComponent<ShiftBriefingUI>();
        }
    }
}
