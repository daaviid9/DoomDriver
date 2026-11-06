using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoomDriver.Economy
{
    /// <summary>
    /// Manages generation of delivery orders and selection by the player.
    /// Follows SRP: no UI, no direct wallet interaction.
    /// </summary>
    public class OrderManager : MonoBehaviour
    {
        public static OrderManager Instance { get; private set; }

        [Header("Difficulty Profiles")]
        [SerializeField] private OrderDifficultyProfile easyProfile;
        [SerializeField] private OrderDifficultyProfile mediumProfile;
        [SerializeField] private OrderDifficultyProfile hardProfile;

        /// <summary>
        /// Event raised when three new offers are generated.
        /// </summary>
        public event Action<IReadOnlyList<DeliveryOrder>> OnOffersGenerated;

        /// <summary>
        /// Event raised when the player selects an order.
        /// </summary>
        public event Action<DeliveryOrder> OnOrderSelected;

        public IReadOnlyList<DeliveryOrder> CurrentOffers { get; private set; }
        public DeliveryOrder SelectedOrder { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Debug.Log("<color=cyan>[OrderManager]</color> Awake initialized.");
        }

        private void OnEnable()
        {
             if (GameManager.Instance != null)
            {
                GameManager.Instance.OnOrderSelectionStarted -= GenerateOffers;
                GameManager.Instance.OnOrderSelectionStarted += GenerateOffers;
                Debug.Log("<color=cyan>[OrderManager]</color> Subscribed to GameManager.OnOrderSelectionStarted.");
            }
        }

        private void Start()
        {
            
            // If game is already in OrderSelection state at start and no offers exist, generate them now
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.OrderSelection)
            {
                if (CurrentOffers == null || CurrentOffers.Count == 0)
                {
                    Debug.Log("<color=cyan>[OrderManager]</color> Start detected OrderSelection state with no offers. Generating now...");
                    GenerateOffers();
                }
            }
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnOrderSelectionStarted -= GenerateOffers;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Generates three procedural orders using the difficulty profiles.
        /// </summary>
        public void GenerateOffers()
        {
            Debug.Log($"<color=cyan>[OrderManager]</color> GenerateOffers called. Profiles: Easy={(easyProfile != null)}, Medium={(mediumProfile != null)}, Hard={(hardProfile != null)}");

            var offers = new List<DeliveryOrder>(3);

            for (int i = 0; i < 3; i++)
            {
                DifficultyLevel difficulty = (DifficultyLevel)i;
                OrderDifficultyProfile profile = GetProfileForSlot(i);
                DeliveryOrder order = CreateProceduralOrder(i, difficulty, profile);
                offers.Add(order);
                Debug.Log($"<color=cyan>[OrderManager]</color> Offer [{i}]: {order.RestaurantName} -> {order.CustomerName} | {order.TotalDistance:F0}m | {order.BasePayout:F0}€ | {order.Difficulty}");
            }

            CurrentOffers = offers;

            int listenerCount = OnOffersGenerated != null ? OnOffersGenerated.GetInvocationList().Length : 0;
            Debug.Log($"<color=cyan>[OrderManager]</color> Invoking OnOffersGenerated with {offers.Count} offers. Active listeners: {listenerCount}");
            OnOffersGenerated?.Invoke(offers);
        }

        private OrderDifficultyProfile GetProfileForSlot(int slotIndex) => slotIndex switch
        {
            0 => easyProfile,
            1 => mediumProfile,
            _ => hardProfile
        };

        private DeliveryOrder CreateProceduralOrder(int slotIndex, DifficultyLevel difficulty, OrderDifficultyProfile profile)
        {
            string orderId = Guid.NewGuid().ToString();

            if (profile != null)
            {
                string restaurant = GetRandomFromArray(profile.RestaurantNames) ?? GetDefaultRestaurant(slotIndex);
                string customer = GetRandomFromArray(profile.CustomerNames) ?? GetDefaultCustomer(slotIndex);
                float distance = Mathf.Round(UnityEngine.Random.Range(profile.DistanceRange.x, profile.DistanceRange.y));
                int turnCount = UnityEngine.Random.Range(profile.TurnCountRange.x, profile.TurnCountRange.y + 1);
                int[] turnSeq = GenerateTurnSequence(turnCount);
                float timeLimit = Mathf.Round(UnityEngine.Random.Range(profile.TimeLimitRange.x, profile.TimeLimitRange.y));
                float basePayout = Mathf.Round(UnityEngine.Random.Range(profile.BasePayoutRange.x, profile.BasePayoutRange.y));
                float maxTip = Mathf.Round(UnityEngine.Random.Range(profile.MaxTipRange.x, profile.MaxTipRange.y));

                return new DeliveryOrder(orderId, restaurant, customer, distance, turnCount, turnSeq, timeLimit, basePayout, maxTip, difficulty);
            }

            Debug.LogWarning($"<color=yellow>[OrderManager]</color> Profile for slot {slotIndex} ({difficulty}) is null! Using built-in fallback values.");
            return new DeliveryOrder(
                orderId,
                GetDefaultRestaurant(slotIndex),
                GetDefaultCustomer(slotIndex),
                400f * (slotIndex + 1),
                1 + slotIndex,
                GenerateTurnSequence(1 + slotIndex),
                30f + (slotIndex * 30f),
                10f + (slotIndex * 8f),
                4f + (slotIndex * 3f),
                difficulty
            );
        }

        /// <summary>
        /// Called by UI when player selects an order.
        /// </summary>
        public void SelectOrder(int index)
        {
            if (CurrentOffers == null || index < 0 || index >= CurrentOffers.Count)
            {
                Debug.LogWarning($"<color=yellow>[OrderManager]</color> Cannot select order {index}: CurrentOffers is invalid.");
                return;
            }

            SelectedOrder = CurrentOffers[index];
            Debug.Log($"<color=green>[OrderManager]</color> Selected order [{index}]: {SelectedOrder.RestaurantName} ({SelectedOrder.BasePayout:F0}€). Starting delivery...");
            OnOrderSelected?.Invoke(SelectedOrder);

            // Notify GameManager to transition to active delivery
            GameManager.Instance?.StartDelivery();
        }

        private static string GetRandomFromArray(string[] array)
        {
            if (array == null || array.Length == 0) return null;
            return array[UnityEngine.Random.Range(0, array.Length)];
        }

        private static string GetDefaultRestaurant(int index) => index switch
        {
            0 => "Pizza Pronto",
            1 => "Burger Bistro",
            _ => "Dragon Wok"
        };

        private static string GetDefaultCustomer(int index) => index switch
        {
            0 => "David M.",
            1 => "Elena K.",
            _ => "Viktor S."
        };

        private static int[] GenerateTurnSequence(int count)
        {
            var seq = new int[count];
            for (int i = 0; i < count; i++)
            {
                // 0 = straight, 1 = left, 2 = right
                seq[i] = UnityEngine.Random.Range(0, 3);
            }
            return seq;
        }
    }
}
