using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DoomDriver.Economy;

namespace DoomDriver.Phone
{
    /// <summary>
    /// UI controller for the courier app shown in the lobby.
    /// Subscribes to OrderManager.OnOffersGenerated and populates order cards.
    /// Connects spawned order cards into the modular PhoneInputNavigator.
    /// </summary>
    public class CourierAppUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Canvas component for the courier app. If null, automatically resolved from this GameObject.")]
        [SerializeField] private Canvas appCanvas;

        [Tooltip("Parent object that holds the card UI objects")]
        [SerializeField] private Transform cardsContainer;

        [Tooltip("Prefab for a single order card, containing OrderCardView component")]
        [SerializeField] private GameObject cardPrefab;

        [Header("Navigation Integration")]
        [Tooltip("Optional reference to PhoneInputNavigator. If null, resolved automatically.")]
        [SerializeField] private PhoneInputNavigator navigator;

        [Header("Shift & Day Progress Display")]
        [Tooltip("Header text displaying day and title, e.g. 'Day 1 • Deliveries'")]
        [SerializeField] private TMP_Text headerTitleText;

        [Tooltip("Subtitle text displaying quota, e.g. 'Daily Goal: 0 / 3 Orders'")]
        [SerializeField] private TMP_Text shiftQuotaText;

        /// <summary>
        /// Event fired whenever new order cards are spawned and ready.
        /// </summary>
        public event System.Action<IReadOnlyList<OrderCardView>> OnCardsPopulated;

        /// <summary>
        /// Read-only access to currently active spawned card views.
        /// </summary>
        public IReadOnlyList<OrderCardView> CardViews => cardViews;

        private readonly List<OrderCardView> cardViews = new List<OrderCardView>();

        private void Awake()
        {
            if (appCanvas == null)
            {
                appCanvas = GetComponent<Canvas>();
            }

            if (appCanvas != null && appCanvas.worldCamera == null)
            {
                appCanvas.worldCamera = Camera.main;
            }

            if (cardsContainer == null)
            {
                Transform found = transform.Find("CardsContainer");
                cardsContainer = found != null ? found : this.transform;
            }

            if (navigator == null)
            {
                navigator = GetComponent<PhoneInputNavigator>() ?? GetComponentInParent<PhoneInputNavigator>();
            }

            AutoResolveHeaderReferences();

            Debug.Log($"<color=magenta>[CourierAppUI]</color> Awake on '{gameObject.name}'. Canvas: {(appCanvas != null ? appCanvas.name : "NULL")}, Container: {(cardsContainer != null ? cardsContainer.name : "NULL")}, Prefab: {(cardPrefab != null ? cardPrefab.name : "NULL")}");
        }

        private void OnEnable()
        {
            Subscribe();
            ShiftManager.OnDeliveryCountChanged += HandleShiftDeliveryCountChanged;
            ShiftManager.OnDayAdvanced += HandleShiftDayAdvanced;
            ShiftManager.OnShiftStarted += HandleShiftStarted;

            RefreshShiftHeader();

            if (OrderManager.Instance != null && OrderManager.Instance.CurrentOffers != null && OrderManager.Instance.CurrentOffers.Count > 0)
            {
                PopulateOffers(OrderManager.Instance.CurrentOffers);
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            ShiftManager.OnDeliveryCountChanged -= HandleShiftDeliveryCountChanged;
            ShiftManager.OnDayAdvanced -= HandleShiftDayAdvanced;
            ShiftManager.OnShiftStarted -= HandleShiftStarted;
        }

        private void Start()
        {
            Subscribe();
            RefreshShiftHeader();

            // Ensure canvas visibility matches the active game state
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.OrderSelection)
            {
                SetAppVisible(false);
            }
            else
            {
                SetAppVisible(true);
            }

            // If offers already exist in OrderManager when UI starts, populate them immediately
            if (OrderManager.Instance != null)
            {
                if (OrderManager.Instance.CurrentOffers != null && OrderManager.Instance.CurrentOffers.Count > 0)
                {
                    Debug.Log($"<color=magenta>[CourierAppUI]</color> Start: Found {OrderManager.Instance.CurrentOffers.Count} existing offers in OrderManager. Populating immediately.");
                    PopulateOffers(OrderManager.Instance.CurrentOffers);
                }
                else if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.OrderSelection)
                {
                    Debug.Log("<color=magenta>[CourierAppUI]</color> Start: In OrderSelection but no offers in OrderManager. Requesting GenerateOffers()...");
                    OrderManager.Instance.GenerateOffers();
                }
            }
            else
            {
                Debug.LogWarning("<color=yellow>[CourierAppUI]</color> Start: OrderManager.Instance is null! Is OrderManager present in the scene?");
            }
        }

        private void Subscribe()
        {
            if (OrderManager.Instance != null)
            {
                OrderManager.Instance.OnOffersGenerated -= PopulateOffers;
                OrderManager.Instance.OnOffersGenerated += PopulateOffers;
                Debug.Log("<color=magenta>[CourierAppUI]</color> Subscribed to OrderManager.OnOffersGenerated.");
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnOrderSelectionStarted -= HandleOrderSelectionStarted;
                GameManager.Instance.OnOrderSelectionStarted += HandleOrderSelectionStarted;

                GameManager.Instance.OnDeliveryStarted -= HandleDeliveryStarted;
                GameManager.Instance.OnDeliveryStarted += HandleDeliveryStarted;
            }
        }

        private void Unsubscribe()
        {
            if (OrderManager.Instance != null)
            {
                OrderManager.Instance.OnOffersGenerated -= PopulateOffers;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnOrderSelectionStarted -= HandleOrderSelectionStarted;
                GameManager.Instance.OnDeliveryStarted -= HandleDeliveryStarted;
            }
        }

        private void HandleOrderSelectionStarted()
        {
            Debug.Log("<color=magenta>[CourierAppUI]</color> GameManager.OnOrderSelectionStarted received. Showing courier app.");
            SetAppVisible(true);

            if (OrderManager.Instance != null && OrderManager.Instance.CurrentOffers != null && OrderManager.Instance.CurrentOffers.Count > 0)
            {
                PopulateOffers(OrderManager.Instance.CurrentOffers);
            }
        }

        private void HandleDeliveryStarted()
        {
            Debug.Log("<color=magenta>[CourierAppUI]</color> GameManager.OnDeliveryStarted received. Hiding courier app.");
            SetAppVisible(false);

            if (navigator != null)
            {
                navigator.ClearNavigableItems();
            }
        }

        /// <summary>
        /// Controls rendering of the courier app canvas and all its elements.
        /// </summary>
        public void SetAppVisible(bool isVisible)
        {
            if (PhoneOSController.Instance != null)
            {
                // When PhoneOSController is present, it manages OS canvas visibility centrally
                return;
            }

            if (appCanvas != null)
            {
                appCanvas.enabled = isVisible;
            }
        }

        private void PopulateOffers(IReadOnlyList<DeliveryOrder> offers)
        {
            Debug.Log($"<color=magenta>[CourierAppUI]</color> PopulateOffers received {offers?.Count ?? 0} offers.");

            if (cardsContainer == null)
            {
                Debug.LogError("<color=red>[CourierAppUI]</color> cardsContainer is NULL! Cannot instantiate cards.");
                return;
            }

            if (cardPrefab == null)
            {
                Debug.LogError("<color=red>[CourierAppUI]</color> cardPrefab is NULL! Assign the OrderCard prefab to CourierAppUI.");
                return;
            }

            if (offers == null || offers.Count == 0)
            {
                Debug.LogWarning("<color=yellow>[CourierAppUI]</color> Received empty or null offers list.");
                cardViews.Clear();
                OnCardsPopulated?.Invoke(cardViews);
                if (navigator != null) navigator.ClearNavigableItems();
                return;
            }

            // Clear existing spawned cards
            for (int i = cardsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(cardsContainer.GetChild(i).gameObject);
            }
            cardViews.Clear();

            for (int i = 0; i < offers.Count; i++)
            {
                var order = offers[i];
                GameObject cardObj = Instantiate(cardPrefab, cardsContainer);
                cardObj.transform.localScale = Vector3.one;
                cardObj.name = $"OrderCard_{i}_{order.Difficulty}";

                var view = cardObj.GetComponent<OrderCardView>();
                if (view == null)
                {
                    Debug.LogWarning($"<color=yellow>[CourierAppUI]</color> cardPrefab is missing OrderCardView component, adding one dynamically.");
                    view = cardObj.AddComponent<OrderCardView>();
                }

                view.Setup(order, i);
                cardViews.Add(view);
                Debug.Log($"<color=magenta>[CourierAppUI]</color> Spawned card {i}: {cardObj.name}");
            }

            Debug.Log($"<color=magenta>[CourierAppUI]</color> Successfully populated {cardViews.Count} order cards. Registering with PhoneInputNavigator.");
            OnCardsPopulated?.Invoke(cardViews);

            // Register items generically into PhoneInputNavigator if CourierApp is active or OS controller not present
            if (navigator != null)
            {
                if (PhoneOSController.Instance == null || PhoneOSController.Instance.CurrentScreen == PhoneScreen.CourierApp)
                {
                    navigator.SetNavigableItems(cardViews, 0);
                }
            }
        }

        private void AutoResolveHeaderReferences()
        {
            if (headerTitleText == null || shiftQuotaText == null)
            {
                var texts = GetComponentsInChildren<TMP_Text>(true);
                foreach (var t in texts)
                {
                    if (headerTitleText == null && t.gameObject.name == "HeaderText")
                    {
                        headerTitleText = t;
                    }
                    else if (shiftQuotaText == null && t.gameObject.name == "DateText")
                    {
                        shiftQuotaText = t;
                    }
                }
            }
        }

        /// <summary>
        /// Refreshes the top header showing day number and current shift progress.
        /// </summary>
        public void RefreshShiftHeader()
        {
            AutoResolveHeaderReferences();

            int day = ShiftManager.Instance != null ? ShiftManager.Instance.CurrentDay : 1;
            int delivered = ShiftManager.Instance != null ? ShiftManager.Instance.DeliveriesCompletedToday : 0;
            int quota = ShiftManager.Instance != null ? ShiftManager.Instance.DailyQuota : 3;
            bool isQuotaMet = ShiftManager.Instance != null && ShiftManager.Instance.IsQuotaMet;

            if (headerTitleText != null)
            {
                headerTitleText.text = $"Day {day} • Deliveries";
            }

            Debug.Log($"<color=magenta>[CourierAppUI]</color> 📋 Refreshed Header -> Day {day} • Orders: {delivered}/{quota} (QuotaMet: {isQuotaMet})");

            if (shiftQuotaText != null)
            {
                if (isQuotaMet)
                {
                    shiftQuotaText.text = $"Quota Met: {delivered} / {quota} Orders (Overtime)";
                    shiftQuotaText.color = new Color(0.2f, 0.95f, 0.35f);
                }
                else
                {
                    shiftQuotaText.text = $"Daily Quota: {delivered} / {quota} Orders";
                    shiftQuotaText.color = new Color(0.9f, 0.9f, 0.9f);
                }
            }
        }

        private void HandleShiftDeliveryCountChanged(int delivered, int quota)
        {
            RefreshShiftHeader();
        }

        private void HandleShiftDayAdvanced(int newDay)
        {
            RefreshShiftHeader();
        }

        private void HandleShiftStarted()
        {
            RefreshShiftHeader();
        }
    }
}
