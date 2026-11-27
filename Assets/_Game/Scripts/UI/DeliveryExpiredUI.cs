using DoomDriver.Economy;

namespace DoomDriver.UI
{
    /// <summary>
    /// Presentation (View) for the Delivery Expired / Late Delivery screen (timer expires).
    /// </summary>
    public class DeliveryExpiredUI : DeliveryFailurePanelUI
    {
        protected override FailureReason HandledReason => FailureReason.TimeExpired;
    }
}
