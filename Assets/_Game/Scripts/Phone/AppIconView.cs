using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace DoomDriver.Phone
{
    /// <summary>
    /// Represents an interactive application launcher icon on the phone's Home Screen.
    /// Implements IPhoneNavigable so PhoneInputNavigator can navigate and launch it seamlessly.
    /// </summary>
    public class AppIconView : MonoBehaviour, IPhoneNavigable, IPointerEnterHandler, IPointerClickHandler
    {
        [Header("App Definition")]
        [Tooltip("The screen/app to open when this icon is selected")]
        [SerializeField] private PhoneScreen targetScreen = PhoneScreen.CourierApp;

        [Tooltip("Optional text label for the app title")]
        [SerializeField] private TMP_Text appTitleText;

        [Tooltip("Optional text label for app subtitle or description (e.g. '3 orders nearby')")]
        [SerializeField] private TMP_Text subtitleText;

        [Tooltip("Icon image component")]
        [SerializeField] private Image iconImage;

        [Header("Notification Badge (Optional)")]
        [Tooltip("Optional notification badge GameObject (e.g. red circular badge)")]
        [SerializeField] private GameObject badgeObject;

        [Tooltip("Optional text inside the badge (e.g. '3' or '!')")]
        [SerializeField] private TMP_Text badgeText;

        [Header("Focus Visual Settings")]
        [Tooltip("Scale multiplier when this app icon is focused/hovered")]
        [SerializeField] private float focusedScale = 1.12f;

        [Tooltip("Speed of scale interpolation")]
        [SerializeField] private float scaleSpeed = 14f;

        [Tooltip("Optional outline component on the icon")]
        [SerializeField] private Outline iconOutline;

        [Tooltip("Outline color when focused")]
        [SerializeField] private Color focusedOutlineColor = new Color(1f, 1f, 1f, 0.9f);

        [Tooltip("Outline color when normal/unfocused")]
        [SerializeField] private Color normalOutlineColor = new Color(0f, 0f, 0f, 0.35f);

        /// <summary>
        /// IPhoneNavigable event fired when mouse hovers over this app icon.
        /// </summary>
        public event System.Action<IPhoneNavigable> OnHovered;

        public PhoneScreen TargetScreen => targetScreen;

        private bool isFocused = false;
        private float currentScale = 1f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            AutoResolveComponents();
        }
#endif

        private void Awake()
        {
            AutoResolveComponents();
        }

        private void AutoResolveComponents()
        {
            if (iconOutline == null)
            {
                iconOutline = GetComponent<Outline>() ?? GetComponentInChildren<Outline>();
            }

            if (iconImage == null)
            {
                iconImage = GetComponent<Image>();
            }

            if (appTitleText == null)
            {
                var titleTransform = transform.Find("AppTitleText") ?? transform.Find("Title") ?? transform.Find("Text");
                if (titleTransform != null) appTitleText = titleTransform.GetComponent<TMP_Text>();
                else appTitleText = GetComponentInChildren<TMP_Text>();
            }

            if (subtitleText == null)
            {
                var subTransform = transform.Find("SubtitleText") ?? transform.Find("Subtitle") ?? transform.Find("Description");
                if (subTransform != null) subtitleText = subTransform.GetComponent<TMP_Text>();
            }

            if (badgeObject == null)
            {
                var badgeTransform = transform.Find("Badge") ?? transform.Find("NotificationBadge");
                if (badgeTransform != null)
                {
                    badgeObject = badgeTransform.gameObject;
                    if (badgeText == null) badgeText = badgeTransform.GetComponentInChildren<TMP_Text>();
                }
            }
        }

        /// <summary>
        /// Updates the badge visibility and count/alert text dynamically.
        /// </summary>
        public void SetBadge(bool visible, string text = "")
        {
            if (badgeObject != null)
            {
                badgeObject.SetActive(visible);
            }

            if (badgeText != null && !string.IsNullOrEmpty(text))
            {
                badgeText.text = text;
            }
        }

        private void Update()
        {
            float target = isFocused ? focusedScale : 1f;
            if (Mathf.Abs(currentScale - target) > 0.001f)
            {
                currentScale = Mathf.Lerp(currentScale, target, Time.unscaledDeltaTime * scaleSpeed);
                transform.localScale = Vector3.one * currentScale;
            }
        }

        /// <summary>
        /// IPhoneNavigable implementation: sets visual focus state.
        /// </summary>
        public void SetFocused(bool focused)
        {
            isFocused = focused;

            if (iconOutline != null)
            {
                iconOutline.effectColor = focused ? focusedOutlineColor : normalOutlineColor;
            }
        }

        /// <summary>
        /// IPhoneNavigable implementation: opens the target app via PhoneOSController.
        /// </summary>
        public void Select()
        {
            Debug.Log($"<color=cyan>[AppIconView]</color> App icon clicked/selected: {targetScreen}");

            var controller = PhoneOSController.Instance ?? GetComponentInParent<PhoneOSController>() ?? FindObjectOfType<PhoneOSController>();
            if (controller != null)
            {
                controller.OpenScreen(targetScreen);
            }
            else
            {
                Debug.LogWarning($"<color=yellow>[AppIconView]</color> PhoneOSController.Instance is null! Make sure the PhoneOSController script is added as a component to PhoneOSCanvas.");
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnHovered?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Select();
        }
    }
}
