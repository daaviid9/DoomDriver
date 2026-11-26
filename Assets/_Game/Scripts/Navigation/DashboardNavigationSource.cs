using System;
using UnityEngine;

/// <summary>Presentation boundary for Dev 4: publish route snapshots here, not directly to UI.</summary>
public sealed class DashboardNavigationSource : MonoBehaviour
{
    public enum Maneuver { Left, Right, Straight, ForkLeft, ForkRight }
    public enum NavigationState { NoRoute, Navigating, Recalculating }

    public NavigationState State { get; private set; }
    public Maneuver Direction { get; private set; }
    public string ManeuverId { get; private set; }
    public float RemainingMeters { get; private set; }
    public event Action Changed;

    /// <param name="maneuverId">Unique within the delivery, including replacement routes.</param>
    public void Publish(string maneuverId, Maneuver direction, float remainingMeters)
    {
        if (string.IsNullOrEmpty(maneuverId) || float.IsNaN(remainingMeters) || float.IsInfinity(remainingMeters))
        {
            ClearRoute();
            return;
        }

        ManeuverId = maneuverId;
        Direction = direction;
        RemainingMeters = Mathf.Max(0f, remainingMeters);
        State = NavigationState.Navigating;
        Changed?.Invoke();
    }

    /// <summary>
    /// Called by junction detection with the actual exit taken, not by the UI.
    /// Returns false for stale/duplicate reports. Incorrect exits automatically enter Recalculating.
    /// The backend remains responsible for generating a replacement route and recording penalties.
    /// </summary>
    public bool ReportTurnTaken(string maneuverId, Maneuver actualDirection)
    {
        if (State != NavigationState.Navigating || string.IsNullOrEmpty(maneuverId) || maneuverId != ManeuverId)
            return false;

        bool isLeft = (actualDirection == Maneuver.Left || actualDirection == Maneuver.ForkLeft);
        bool isRight = (actualDirection == Maneuver.Right || actualDirection == Maneuver.ForkRight);
        bool isStraight = (actualDirection == Maneuver.Straight);

        if (!isLeft && !isRight && !isStraight)
            return false;

        bool expectedIsLeft = (Direction == Maneuver.Left || Direction == Maneuver.ForkLeft);
        bool expectedIsRight = (Direction == Maneuver.Right || Direction == Maneuver.ForkRight);

        bool isMatch = (actualDirection == Direction) || (expectedIsLeft && isLeft) || (expectedIsRight && isRight);

        if (isMatch) ClearRoute();
        else SetRecalculating();
        return true;
    }

    public void SetRecalculating()
    {
        State = NavigationState.Recalculating;
        Changed?.Invoke();
    }

    public void ClearRoute()
    {
        State = NavigationState.NoRoute;
        ManeuverId = null;
        RemainingMeters = 0f;
        Changed?.Invoke();
    }
}
