using System;
using UnityEngine;

/// <summary>Presentation boundary for Dev 4: publish route snapshots here, not directly to UI.</summary>
public sealed class DashboardNavigationSource : MonoBehaviour
{
    public enum Maneuver { Left, Right, Straight }
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
