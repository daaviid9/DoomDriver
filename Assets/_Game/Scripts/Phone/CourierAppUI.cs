using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DoomDriver.Economy;

namespace DoomDriver.Phone
{
    /// <summary>
    /// UI controller for the courier app shown in the lobby.
    /// Subscribes to OrderManager.OnOffersGenerated and populates three order cards.
    /// </summary>
    public class CourierAppUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Parent object that holds the three card UI objects")]
        [SerializeField] private Transform cardsContainer;
        [Tooltip("Prefab for a single order card, containing OrderCardView component")]
        [SerializeField] private GameObject cardPrefab;

        private readonly List<OrderCardView> cardViews = new List<OrderCardView>();

        private void Awake()
        {
            if (cardsContainer == null)
            {
                Transform found = transform.Find("CardsContainer");
                cardsContainer = found != null ? found : this.transform;
            }

            Debug.Log($"<color=magenta>[CourierAppUI]</color> Awake on '{gameObject.name}'. Container: {(cardsContainer != null ? cardsContainer.name : "NULL")}, Prefab: {(cardPrefab != null ? cardPrefab.name : "NULL")}");
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Start()
        {
            Subscribe();
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
            Debug.Log("<color=magenta>[CourierAppUI]</color> GameManager.OnOrderSelectionStarted received.");
            if (cardsContainer != null) cardsContainer.gameObject.SetActive(true);

            if (OrderManager.Instance != null && OrderManager.Instance.CurrentOffers != null && OrderManager.Instance.CurrentOffers.Count > 0)
            {
                PopulateOffers(OrderManager.Instance.CurrentOffers);
            }
        }

        private void HandleDeliveryStarted()
        {
            Debug.Log("<color=magenta>[CourierAppUI]</color> GameManager.OnDeliveryStarted received. Hiding cards container.");
            if (cardsContainer != null) cardsContainer.gameObject.SetActive(false);
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

            Debug.Log($"<color=magenta>[CourierAppUI]</color> Successfully populated {cardViews.Count} order cards inside '{cardsContainer.name}'.");

        }
    }
}

