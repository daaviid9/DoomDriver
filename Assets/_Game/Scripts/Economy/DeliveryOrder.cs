using System;
using UnityEngine;

namespace DoomDriver.Economy
{
    /// <summary>
    /// Immutable data class that represents a delivery order.
    /// </summary>
    [Serializable]
    public sealed class DeliveryOrder
    {
        public string OrderId { get; }
        public string RestaurantName { get; }
        public string CustomerName { get; }
        public float TotalDistance { get; }
        public int TurnCount { get; }
        public int[] TurnSequence { get; }
        public float TimeLimit { get; }
        public float BasePayout { get; }
        public float MaxTip { get; }
        public DifficultyLevel Difficulty { get; }

        public DeliveryOrder(
            string orderId,
            string restaurantName,
            string customerName,
            float totalDistance,
            int turnCount,
            int[] turnSequence,
            float timeLimit,
            float basePayout,
            float maxTip,
            DifficultyLevel difficulty)
        {
            OrderId = orderId;
            RestaurantName = restaurantName;
            CustomerName = customerName;
            TotalDistance = totalDistance;
            TurnCount = turnCount;
            TurnSequence = turnSequence ?? System.Array.Empty<int>();
            TimeLimit = timeLimit;
            BasePayout = basePayout;
            MaxTip = maxTip;
            Difficulty = difficulty;
        }
    }

    /// <summary>
    /// Simple enumeration for difficulty categories.
    /// </summary>
    public enum DifficultyLevel
    {
        Easy,
        Medium,
        Hard
    }
}

