using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DoomDriver.Economy;

namespace DoomDriver.Phone
{
    public enum PhoneScreen
    {
        Home,
        CourierApp,
        ShopApp,
        BillsApp,
        DoomTok
    }

    /// <summary>
    /// Central manager for the in-game smartphone operating system ("DoomOS").
    /// Controls screen transitions between Home Screen and installed Apps (Courier, Shop, Bills),
    /// updates the status bar clock, and injects active navigation contexts into PhoneInputNavigator.
    /// </summary>
    public class PhoneOSController : MonoBehaviour
    {
        public static PhoneOSController Instance { get; private set; }

        [Header("Screens & Panels")]
        [Tooltip("Root panel of the Home Screen (contains app icons)")]
        [SerializeField] private GameObject homeScreenPanel;

        [Tooltip("Root panel of the Courier app (order cards)")]
        [SerializeField] private GameObject courierAppPanel;

        [Tooltip("Root panel of the Courier Upgrade Shop")]
        [SerializeField] private GameObject shopAppPanel;

        [Tooltip("Root panel of the Bills & Living Expenses app")]
        [SerializeField] private GameObject billsAppPanel;

        [Header("Status Bar")]
        [Tooltip("Text component displaying the current digital time")]
        [SerializeField] private TMP_Text clockText;

        [Header("Core Dependencies")]
        [Tooltip("Reference to the PhoneInputNavigator. If null, resolved automatically.")]
        [SerializeField] private PhoneInputNavigator navigator;

        [Tooltip("Reference to CourierAppUI on the Courier panel.")]
        [SerializeField] private CourierAppUI courierAppUI;

        [Tooltip("The root World Space Canvas for the phone OS")]
        [SerializeField] private Canvas osCanvas;

        [Header("Audio & Effects (Optional)")]
        [Tooltip("Audio source for phone click / tap sounds")]
        [SerializeField] private AudioSource phoneAudioSource;

        [Tooltip("Sound played when opening an app")]
        [SerializeField] private AudioClip openAppClip;

        [Tooltip("Sound played when pressing back to home")]
        [SerializeField] private AudioClip backHomeClip;

        /// <summary>
        /// Event fired whenever the active phone screen changes.
        /// </summary>
        public event System.Action<PhoneScreen> OnScreenChanged;

        public PhoneScreen CurrentScreen { get; private set; } = PhoneScreen.Home;

        private int lastSelectedHomeIndex = 0;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(this);
                return;
            }

            AutoResolveDependencies();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void Start()
        {
            SubscribeToEvents();

            // Set initial state: In lobby start on Home Screen, during delivery hide OS
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DeliveryActive)
            {
                SetOSVisible(false);
            }
            else
            {
                SetOSVisible(true);
                OpenScreen(PhoneScreen.Home);
            }
        }

        private void Update()
        {
            UpdateClock();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            AutoResolveDependencies();
        }
#endif

        private void AutoResolveDependencies()
        {
            if (osCanvas == null)
            {
                osCanvas = GetComponent<Canvas>() ?? GetComponentInParent<Canvas>();
            }

            if (navigator == null)
            {
                navigator = GetComponent<PhoneInputNavigator>() ?? GetComponentInChildren<PhoneInputNavigator>(true);
            }

            if (courierAppUI == null)
            {
                courierAppUI = GetComponentInChildren<CourierAppUI>(true);
            }

            if (homeScreenPanel == null)
            {
                var t = transform.Find("HomeScreenPanel") ?? transform.Find("HomeScreen");
                if (t != null) homeScreenPanel = t.gameObject;
            }

            if (courierAppPanel == null)
            {
                var t = transform.Find("CourierAppPanel") ?? transform.Find("CourierApp") ?? transform.Find("CardsContainer");
                if (t != null) courierAppPanel = t.gameObject;
                else if (courierAppUI != null && courierAppUI.gameObject != this.gameObject)
                {
                    courierAppPanel = courierAppUI.gameObject;
                }
            }

            if (shopAppPanel == null)
            {
                var t = transform.Find("ShopAppPanel") ?? transform.Find("ShopApp");
                if (t != null) shopAppPanel = t.gameObject;
            }

            if (billsAppPanel == null)
            {
                var t = transform.Find("BillsAppPanel") ?? transform.Find("BillsApp");
                if (t != null) billsAppPanel = t.gameObject;
            }

            if (clockText == null)
            {
                var t = transform.Find("StatusBar/TimeText") ?? transform.Find("StatusBar/ClockText") ?? transform.Find("StatusBar/Clock") ?? transform.Find("TimeText") ?? transform.Find("ClockText");
                if (t != null) clockText = t.GetComponent<TMP_Text>();
                else
                {
                    var statusBar = transform.Find("StatusBar");
                    if (statusBar != null) clockText = statusBar.GetComponentInChildren<TMP_Text>();
                }
            }
        }

        private void SubscribeToEvents()
        {
            if (navigator != null)
            {
                navigator.OnBackRequested -= HandleBackRequested;
                navigator.OnBackRequested += HandleBackRequested;
            }

            if (courierAppUI != null)
            {
                courierAppUI.OnCardsPopulated -= HandleCourierCardsPopulated;
                courierAppUI.OnCardsPopulated += HandleCourierCardsPopulated;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnDayBriefingStarted -= HandleOrderSelectionStarted;
                GameManager.Instance.OnDayBriefingStarted += HandleOrderSelectionStarted;

                GameManager.Instance.OnOrderSelectionStarted -= HandleOrderSelectionStarted;
                GameManager.Instance.OnOrderSelectionStarted += HandleOrderSelectionStarted;

                GameManager.Instance.OnDaySummaryStarted -= HandleOrderSelectionStarted;
                GameManager.Instance.OnDaySummaryStarted += HandleOrderSelectionStarted;

                GameManager.Instance.OnDeliveryStarted -= HandleDeliveryStarted;
                GameManager.Instance.OnDeliveryStarted += HandleDeliveryStarted;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (navigator != null)
            {
                navigator.OnBackRequested -= HandleBackRequested;
            }

            if (courierAppUI != null)
            {
                courierAppUI.OnCardsPopulated -= HandleCourierCardsPopulated;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnDayBriefingStarted -= HandleOrderSelectionStarted;
                GameManager.Instance.OnOrderSelectionStarted -= HandleOrderSelectionStarted;
                GameManager.Instance.OnDaySummaryStarted -= HandleOrderSelectionStarted;
                GameManager.Instance.OnDeliveryStarted -= HandleDeliveryStarted;
            }
        }

        /// <summary>
        /// Switches to the specified application or screen.
        /// </summary>
        public void OpenScreen(PhoneScreen targetScreen)
        {
            if (CurrentScreen == PhoneScreen.Home && navigator != null && navigator.SelectedIndex >= 0)
            {
                lastSelectedHomeIndex = navigator.SelectedIndex;
            }

            // Automatically start shift when opening CourierApp during DayBriefing
            if (targetScreen == PhoneScreen.CourierApp && GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DayBriefing)
            {
                Debug.Log("<color=green>[PhoneOSController]</color> Opening Courier App during DayBriefing -> Starting Shift!");
                if (ShiftManager.Instance != null)
                {
                    ShiftManager.Instance.StartShift();
                }
                else
                {
                    GameManager.Instance.TransitionFromBriefingToShift();
                }
            }

            CurrentScreen = targetScreen;
            Debug.Log($"<color=cyan>[PhoneOSController]</color> Switching screen to: {targetScreen}");

            // Toggle panels
            if (homeScreenPanel != null) homeScreenPanel.SetActive(targetScreen == PhoneScreen.Home);
            if (courierAppPanel != null) courierAppPanel.SetActive(targetScreen == PhoneScreen.CourierApp);
            if (shopAppPanel != null) shopAppPanel.SetActive(targetScreen == PhoneScreen.ShopApp);
            if (billsAppPanel != null) billsAppPanel.SetActive(targetScreen == PhoneScreen.BillsApp);

            // Configure navigation context based on the active screen
            switch (targetScreen)
            {
                case PhoneScreen.Home:
                    SetupHomeNavigation();
                    break;

                case PhoneScreen.CourierApp:
                    SetupCourierNavigation();
                    break;

                case PhoneScreen.ShopApp:
                    SetupShopNavigation();
                    break;

                case PhoneScreen.BillsApp:
                    SetupBillsNavigation();
                    break;
            }

            PlayAudio(openAppClip);
            OnScreenChanged?.Invoke(targetScreen);
        }

        /// <summary>
        /// Returns back to the Home Screen.
        /// </summary>
        public void ReturnToHomeScreen()
        {
            if (CurrentScreen == PhoneScreen.Home) return;

            PlayAudio(backHomeClip);
            OpenScreen(PhoneScreen.Home);
        }

        private void SetupHomeNavigation()
        {
            if (navigator == null || homeScreenPanel == null) return;

            var icons = homeScreenPanel.GetComponentsInChildren<AppIconView>(false);
            if (icons != null && icons.Length > 0)
            {
                // Pass columns: 2 for the 2x2 home screen apps grid
                navigator.SetNavigableItems(icons, lastSelectedHomeIndex, columns: 2);
            }
            else
            {
                navigator.ClearNavigableItems();
            }
        }

        private void SetupCourierNavigation()
        {
            if (navigator == null || courierAppUI == null) return;

            // Ensure offers are generated if OrderManager is empty
            if (OrderManager.Instance != null && (OrderManager.Instance.CurrentOffers == null || OrderManager.Instance.CurrentOffers.Count == 0))
            {
                OrderManager.Instance.GenerateOffers();
            }

            if (courierAppUI.CardViews != null && courierAppUI.CardViews.Count > 0)
            {
                navigator.SetNavigableItems(courierAppUI.CardViews, 0);
            }
            else
            {
                navigator.ClearNavigableItems();
            }
        }

        private void SetupShopNavigation()
        {
            if (navigator == null || shopAppPanel == null) return;

            var shopItems = shopAppPanel.GetComponentsInChildren<IPhoneNavigable>(false);
            if (shopItems != null && shopItems.Length > 0)
            {
                navigator.SetNavigableItems(shopItems, 0);
            }
            else
            {
                navigator.ClearNavigableItems();
            }
        }

        private void SetupBillsNavigation()
        {
            if (navigator == null || billsAppPanel == null) return;

            var billItems = billsAppPanel.GetComponentsInChildren<IPhoneNavigable>(false);
            if (billItems != null && billItems.Length > 0)
            {
                navigator.SetNavigableItems(billItems, 0);
            }
            else
            {
                navigator.ClearNavigableItems();
            }
        }

        private void HandleBackRequested()
        {
            // If inside an app, back navigates to Home
            if (CurrentScreen != PhoneScreen.Home)
            {
                ReturnToHomeScreen();
            }
        }

        private void HandleCourierCardsPopulated(IReadOnlyList<OrderCardView> cards)
        {
            // If player is currently looking at CourierApp, refresh items
            if (CurrentScreen == PhoneScreen.CourierApp && navigator != null)
            {
                navigator.SetNavigableItems(cards, 0);
            }
        }

        private void HandleOrderSelectionStarted()
        {
            SetOSVisible(true);
            OpenScreen(PhoneScreen.Home);
        }

        private void HandleDeliveryStarted()
        {
            SetOSVisible(false);
            if (navigator != null)
            {
                navigator.ClearNavigableItems();
            }
        }

        public void SetOSVisible(bool visible)
        {
            if (osCanvas != null)
            {
                osCanvas.enabled = visible;
            }
        }

        private void UpdateClock()
        {
            if (clockText != null)
            {
                var now = System.DateTime.Now;
                clockText.text = $"{now.Hour:D2}:{now.Minute:D2}";
            }
        }

        private void PlayAudio(AudioClip clip)
        {
            if (phoneAudioSource != null && clip != null)
            {
                phoneAudioSource.PlayOneShot(clip);
            }
        }
    }
}
