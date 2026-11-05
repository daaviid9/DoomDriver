using UnityEngine;

namespace DoomDriver.Economy
{
    /// <summary>
    /// ScriptableObject that defines configurable ranges and name pools for a difficulty level.
    /// Designers can create assets (EasyProfile, MediumProfile, HardProfile) to tweak order generation.
    /// </summary>
    [CreateAssetMenu(fileName = "OrderDifficultyProfile", menuName = "DoomDriver/Economy/Order Difficulty Profile")]
    public class OrderDifficultyProfile : ScriptableObject
    {
        public DifficultyLevel Difficulty;
        public Vector2 DistanceRange = new Vector2(500f, 1500f); // meters
        public Vector2 TimeLimitRange = new Vector2(30f, 120f);   // seconds
        public Vector2 BasePayoutRange = new Vector2(10f, 30f);   // euro
        public Vector2 MaxTipRange = new Vector2(5f, 15f);       // euro
        public Vector2Int TurnCountRange = new Vector2Int(2, 5);
        // Optional pools for random restaurant / customer names
        public string[] RestaurantNames;
        public string[] CustomerNames;
    }
}

