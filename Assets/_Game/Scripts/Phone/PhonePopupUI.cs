using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace DoomDriver.Phone
{
    /// <summary>
    /// Pure presentation (View) controller for a timed toast/popup notice inside DoomOS.
    /// Owns all UI copy per PhoneScreen and automatically hides after <see cref="displayDuration"/> seconds.
    /// Listens to PhoneOSController.OnScreenAccessDenied and can also be triggered directly via Show(message).
    /// </summary>
    public class PhonePopupUI : MonoBehaviour
    {
        [Serializable]
        private struct ScreenMessageEntry
        {
            public PhoneScreen screen;
            [TextArea(2, 4)]
            public string message;
        }

        [Header("UI References")]
        [Tooltip("Root GameObject of the popup panel (toggled active/inactive).")]
        [SerializeField] private GameObject popupRoot;

        [Tooltip("Body message label inside the popup.")]
        [SerializeField] private TMP_Text messageText;

        [Header("Timing")]
        [Tooltip("How many seconds the popup stays visible before hiding automatically.")]
        [SerializeField, Min(0.1f)] private float displayDuration = 2.5f;

        [Header("Per-Screen Messages")]
        [Tooltip("Messages displayed when opening a specific PhoneScreen is denied.")]
        [SerializeField] private List<ScreenMessageEntry> screenMessages = new List<ScreenMessageEntry>
        {
            new ScreenMessageEntry
            {
                screen = PhoneScreen.ShopApp,
                message = "Doom Garage is only accessible at the end of the day."
            },
            new ScreenMessageEntry
            {
                screen = PhoneScreen.BillsApp,
                message = "DoomBank is currently unavailable."
            }
        };

        [Tooltip("Fallback message if no entry is configured for the denied PhoneScreen.")]
        [SerializeField] private string fallbackMessage = "This app is currently unavailable.";

        private PhoneOSController subscribedController;
        private float hideAtUnscaledTime;
        private bool isVisible;

        private void Awake()
        {
            Hide();
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void Start()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            Hide();
        }

        private void Update()
        {
            if (isVisible && Time.unscaledTime >= hideAtUnscaledTime)
            {
                Hide();
            }
        }

        /// <summary>
        /// Displays the popup with <paramref name="message"/> and hides it automatically after <paramref name="durationSeconds"/>
        /// (or <see cref="displayDuration"/> if omitted / <= 0).
        /// </summary>
        public void Show(string message, float durationSeconds = -1f)
        {
            if (popupRoot == null)
            {
                Debug.LogWarning("<color=yellow>[PhonePopupUI]</color> popupRoot is not assigned in Inspector.");
                return;
            }

            if (messageText != null)
            {
                messageText.text = message;
            }

            float duration = durationSeconds > 0f ? durationSeconds : displayDuration;
            hideAtUnscaledTime = Time.unscaledTime + duration;
            isVisible = true;
            popupRoot.SetActive(true);
        }

        public void Hide()
        {
            isVisible = false;
            if (popupRoot != null)
            {
                popupRoot.SetActive(false);
            }
        }

        private void TrySubscribe()
        {
            var controller = PhoneOSController.Instance != null
                ? PhoneOSController.Instance
                : GetComponentInParent<PhoneOSController>();

            if (controller == null || controller == subscribedController) return;

            Unsubscribe();
            subscribedController = controller;
            subscribedController.OnScreenAccessDenied += HandleScreenAccessDenied;
        }

        private void Unsubscribe()
        {
            if (subscribedController == null) return;

            subscribedController.OnScreenAccessDenied -= HandleScreenAccessDenied;
            subscribedController = null;
        }

        private void HandleScreenAccessDenied(PhoneScreen screen)
        {
            Show(ResolveMessage(screen));
        }

        private string ResolveMessage(PhoneScreen screen)
        {
            for (int i = 0; i < screenMessages.Count; i++)
            {
                if (screenMessages[i].screen == screen &&
                    !string.IsNullOrWhiteSpace(screenMessages[i].message))
                {
                    return screenMessages[i].message;
                }
            }

            return fallbackMessage;
        }
    }
}
