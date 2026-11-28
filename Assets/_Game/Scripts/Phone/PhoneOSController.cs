using UnityEngine;
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
    /// and injects active navigation contexts into PhoneInputNavigator.
    /// </summary>
    public class PhoneOSController : MonoBehaviour
    {
        public static PhoneOSController Instance { get; private set; }

        [Header("Screens & Panels")]
        [SerializeField] private GameObject homeScreenPanel;
        [SerializeField] private GameObject courierAppPanel;
        [SerializeField] private GameObject shopAppPanel;
        [SerializeField] private GameObject billsAppPanel;

        [Header("Status Bar")]
        [SerializeField] private TMP_Text clockText;
        [SerializeField] private PhoneClockUI clockUI;

        [Header("Core Dependencies")]
        [SerializeField] private PhoneInputNavigator navigator;
        [SerializeField] private CourierAppUI courierAppUI;
        [SerializeField] private Canvas osCanvas;

        [Header("Audio & Effects (Optional)")]
        [SerializeField] private AudioSource phoneAudioSource;
        [SerializeField] private AudioClip openAppClip;
        [SerializeField] private AudioClip backHomeClip;

        public event System.Action<PhoneScreen> OnScreenChanged;
        public event System.Action<PhoneScreen> OnScreenAccessDenied;

        public PhoneScreen CurrentScreen { get; private set; } = PhoneScreen.Home;
        public bool IsOSVisible => osCanvas != null && osCanvas.enabled;

        private readonly Dictionary<PhoneScreen, GameObject> screenPanels = new Dictionary<PhoneScreen, GameObject>();
        private int lastSelectedHomeIndex = 0;
        private Coroutine hideOSCoroutine;

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
            BuildScreenMap();
            EnsureShopApp();
            EnsureClockUI();
        }

        private void BuildScreenMap()
        {
            screenPanels.Clear();
            if (homeScreenPanel != null) screenPanels[PhoneScreen.Home] = homeScreenPanel;
            if (courierAppPanel != null) screenPanels[PhoneScreen.CourierApp] = courierAppPanel;
            if (shopAppPanel != null) screenPanels[PhoneScreen.ShopApp] = shopAppPanel;
            if (billsAppPanel != null) screenPanels[PhoneScreen.BillsApp] = billsAppPanel;
        }

        private void EnsureShopApp()
        {
            if (shopAppPanel != null && shopAppPanel.GetComponent<ShopAppUI>() == null)
            {
                shopAppPanel.AddComponent<ShopAppUI>();
            }
        }

        private void EnsureClockUI()
        {
            if (clockUI == null)
            {
                clockUI = GetComponent<PhoneClockUI>() ?? gameObject.AddComponent<PhoneClockUI>();
            }
            if (clockText != null)
            {
                clockUI.Bind(clockText);
            }
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            AutoResolveDependencies();
        }
#endif

        private void AutoResolveDependencies()
        {
            if (osCanvas == null) osCanvas = GetComponent<Canvas>() ?? GetComponentInParent<Canvas>();
            if (navigator == null) navigator = GetComponent<PhoneInputNavigator>() ?? GetComponentInChildren<PhoneInputNavigator>(true);
            if (courierAppUI == null) courierAppUI = GetComponentInChildren<CourierAppUI>(true);

            if (homeScreenPanel == null)
            {
                var t = transform.Find("HomeScreenPanel") ?? transform.Find("HomeScreen");
                if (t != null) homeScreenPanel = t.gameObject;
            }

            if (courierAppPanel == null)
            {
                var t = transform.Find("CourierAppPanel") ?? transform.Find("CourierApp") ?? transform.Find("CardsContainer");
                if (t != null) courierAppPanel = t.gameObject;
                else if (courierAppUI != null && courierAppUI.gameObject != gameObject) courierAppPanel = courierAppUI.gameObject;
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
                GameManager.Instance.OnDayBriefingStarted -= ShowHomeScreen;
                GameManager.Instance.OnDayBriefingStarted += ShowHomeScreen;

                GameManager.Instance.OnOrderSelectionStarted -= ShowHomeScreen;
                GameManager.Instance.OnOrderSelectionStarted += ShowHomeScreen;

                GameManager.Instance.OnDaySummaryStarted -= ShowHomeScreen;
                GameManager.Instance.OnDaySummaryStarted += ShowHomeScreen;

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
                GameManager.Instance.OnDayBriefingStarted -= ShowHomeScreen;
                GameManager.Instance.OnOrderSelectionStarted -= ShowHomeScreen;
                GameManager.Instance.OnDaySummaryStarted -= ShowHomeScreen;
                GameManager.Instance.OnDeliveryStarted -= HandleDeliveryStarted;
            }
        }

        public void OpenScreen(PhoneScreen targetScreen)
        {
            GameState? currentState = GameManager.Instance != null ? GameManager.Instance.CurrentState : (GameState?)null;
            if (!PhoneScreenAccessPolicy.CanOpen(targetScreen, currentState))
            {
                Debug.Log($"<color=yellow>[PhoneOSController]</color> Access denied for {targetScreen} in state {currentState}.");
                OnScreenAccessDenied?.Invoke(targetScreen);
                return;
            }

            if (CurrentScreen == PhoneScreen.Home && navigator != null && navigator.SelectedIndex >= 0)
            {
                lastSelectedHomeIndex = navigator.SelectedIndex;
            }

            if (targetScreen == PhoneScreen.CourierApp && GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DayBriefing)
            {
                Debug.Log("<color=green>[PhoneOSController]</color> Opening Courier App during DayBriefing -> Starting Shift!");
                if (ShiftManager.Instance != null) ShiftManager.Instance.StartShift();
                else GameManager.Instance.TransitionFromBriefingToShift();
            }

            CurrentScreen = targetScreen;
            Debug.Log($"<color=cyan>[PhoneOSController]</color> Switching screen to: {targetScreen}");

            foreach (var kvp in screenPanels)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.SetActive(kvp.Key == targetScreen);
                }
            }

            ConfigureNavigationForScreen(targetScreen);
            PlayAudio(openAppClip);
            OnScreenChanged?.Invoke(targetScreen);
        }

        public void ReturnToHomeScreen()
        {
            if (CurrentScreen == PhoneScreen.Home) return;

            Debug.Log("<color=#94A3B8>[PhoneOSController]</color> 🔙 Returning to Home Screen");
            PlayAudio(backHomeClip);
            OpenScreen(PhoneScreen.Home);
        }

        private void ConfigureNavigationForScreen(PhoneScreen screen)
        {
            if (navigator == null) return;

            switch (screen)
            {
                case PhoneScreen.Home:
                    SetupPanelNavigation(homeScreenPanel, lastSelectedHomeIndex, columns: 2);
                    break;

                case PhoneScreen.CourierApp:
                    SetupCourierNavigation();
                    break;

                case PhoneScreen.ShopApp:
                    SetupPanelNavigation(shopAppPanel, 0, columns: 1);
                    break;

                case PhoneScreen.BillsApp:
                    SetupPanelNavigation(billsAppPanel, 0, columns: 1);
                    break;
            }
        }

        private void SetupPanelNavigation(GameObject panel, int initialIndex, int columns)
        {
            if (navigator == null || panel == null) return;

            var items = panel.GetComponentsInChildren<IPhoneNavigable>(false);
            if (items != null && items.Length > 0)
            {
                navigator.SetNavigableItems(items, initialIndex, columns);
            }
            else
            {
                navigator.ClearNavigableItems();
            }
        }

        private void SetupCourierNavigation()
        {
            if (navigator == null || courierAppUI == null) return;

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

        private void HandleBackRequested()
        {
            if (CurrentScreen != PhoneScreen.Home)
            {
                ReturnToHomeScreen();
            }
        }

        private void HandleCourierCardsPopulated(IReadOnlyList<OrderCardView> cards)
        {
            if (CurrentScreen == PhoneScreen.CourierApp && navigator != null)
            {
                navigator.SetNavigableItems(cards, 0);
            }
        }

        private void ShowHomeScreen()
        {
            CancelHideOSCoroutine();
            SetOSVisible(true);
            OpenScreen(PhoneScreen.Home);
        }

        private void HandleDeliveryStarted()
        {
            if (navigator != null)
            {
                navigator.ClearNavigableItems();
            }

            CancelHideOSCoroutine();
            hideOSCoroutine = StartCoroutine(HideOSAfterHandLowers(0.35f));
        }

        private System.Collections.IEnumerator HideOSAfterHandLowers(float delay)
        {
            yield return new WaitForSeconds(delay);
            SetOSVisible(false);
            hideOSCoroutine = null;
        }

        private void CancelHideOSCoroutine()
        {
            if (hideOSCoroutine != null)
            {
                StopCoroutine(hideOSCoroutine);
                hideOSCoroutine = null;
            }
        }

        public void SetOSVisible(bool visible)
        {
            if (osCanvas != null)
            {
                osCanvas.enabled = visible;
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
