/// <summary>
/// Defines the high-level states of the courier delivery gameplay loop.
/// </summary>
public enum GameState
{
    /// <summary>
    /// Lobby in parked car: vehicle is stationary, player selects a delivery order on the phone.
    /// </summary>
    OrderSelection,

    /// <summary>
    /// Active delivery mission: world moves, GPS navigation directs the player, FOMO distraction drains, obstacles spawn.
    /// </summary>
    DeliveryActive,

    /// <summary>
    /// Destination successfully reached in time: car stops, order payout and score are evaluated.
    /// </summary>
    DeliveryCompleted,

    /// <summary>
    /// Delivery failed: vehicle destroyed (0 HP) or critical time expired.
    /// </summary>
    DeliveryFailed
}

