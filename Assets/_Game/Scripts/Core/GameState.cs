/// <summary>
/// Defines the high-level states of the courier delivery gameplay loop and daily shift lifecycle.
/// </summary>
public enum GameState
{
    /// <summary>
    /// Morning briefing in parked car: vehicle is stationary, player reviews daily quota, dispatcher notes, and starts shift.
    /// </summary>
    DayBriefing,

    /// <summary>
    /// Macro state indicating an active working shift is in progress.
    /// </summary>
    ShiftActive,

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
    DeliveryFailed,

    /// <summary>
    /// End-of-day settlement: vehicle is stationary, shows total daily earnings, living expenses/bills deducted, and advances day.
    /// </summary>
    DaySummary
}

