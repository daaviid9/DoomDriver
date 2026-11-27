using DoomDriver.Economy;

namespace DoomDriver.UI
{
    /// <summary>
    /// Presentation (View) for the Delivery Failed / Vehicle Crash screen (HP reaches 0).
    /// </summary>
    public class DeliveryFailedUI : DeliveryFailurePanelUI
    {
        protected override FailureReason HandledReason => FailureReason.VehicleCrash;
    }
}
