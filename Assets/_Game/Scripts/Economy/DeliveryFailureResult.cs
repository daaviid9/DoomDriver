using System;

namespace DoomDriver.Economy
{
    /// <summary>
    /// Identifies why a delivery failed.
    /// </summary>
    public enum FailureReason
    {
        VehicleCrash,
        TimeExpired
    }

    /// <summary>
    /// Immutable data transfer object holding itemized penalty fee amounts for a failed delivery.
    /// Purely holds data for UI presentation and economy tracking.
    /// </summary>
    [Serializable]
    public sealed class DeliveryFailureResult
    {
        public FailureReason Reason { get; }
        public float Fee1 { get; }
        public float Fee2 { get; }
        public float Fee3 { get; }
        public float TotalPenalty => Fee1 + Fee2 + Fee3;

        public DeliveryFailureResult(FailureReason reason, float fee1, float fee2, float fee3)
        {
            Reason = reason;
            Fee1 = fee1;
            Fee2 = fee2;
            Fee3 = fee3;
        }
    }
}

