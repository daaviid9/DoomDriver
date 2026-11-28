using UnityEngine;
using DoomDriver.Economy;

namespace DoomDriver.Core
{
    /// <summary>
    /// Editor-only debug hotkeys for testing game state transitions, shifts, and career resets.
    /// Extracted from GameManager to keep GameManager free of Update() and input polling.
    /// </summary>
    public class GameDebugHotkeys : MonoBehaviour
    {
#if UNITY_EDITOR
        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null || GameManager.Instance == null) return;

            GameState currentState = GameManager.Instance.CurrentState;

            if (keyboard.cKey.wasPressedThisFrame && currentState == GameState.DeliveryActive)
            {
                Debug.Log("<color=green>[DEBUG]</color> C pressed -> Completing Delivery!");
                GameManager.Instance.CompleteDelivery();
            }
            else if (keyboard.rKey.wasPressedThisFrame &&
                    (currentState == GameState.DeliveryCompleted || currentState == GameState.DeliveryFailed))
            {
                Debug.Log("<color=yellow>[DEBUG]</color> R pressed -> Returning to Order Selection Lobby!");
                GameManager.Instance.ReturnToOrderSelection();
            }
            else if ((keyboard.upArrowKey.wasPressedThisFrame ||
                      keyboard.wKey.wasPressedThisFrame ||
                      keyboard.bKey.wasPressedThisFrame) && currentState == GameState.DayBriefing)
            {
                Debug.Log("<color=green>[DEBUG]</color> Up Arrow / W / B pressed -> Starting Shift and opening Order Selection!");
                if (ShiftManager.Instance != null) ShiftManager.Instance.StartShift();
                else GameManager.Instance.TransitionFromBriefingToShift();

                if (DoomDriver.Phone.PhoneOSController.Instance != null)
                {
                    DoomDriver.Phone.PhoneOSController.Instance.OpenScreen(DoomDriver.Phone.PhoneScreen.CourierApp);
                }
            }
            else if (keyboard.nKey.wasPressedThisFrame &&
                    (currentState == GameState.OrderSelection || currentState == GameState.DeliveryCompleted))
            {
                Debug.Log("<color=yellow>[DEBUG]</color> N pressed -> Ending Shift to DaySummary!");
                if (ShiftManager.Instance != null) ShiftManager.Instance.EndShift();
                else GameManager.Instance.OpenDaySummary();
            }
            else if (keyboard.mKey.wasPressedThisFrame && currentState == GameState.DaySummary)
            {
                Debug.Log("<color=cyan>[DEBUG]</color> M pressed -> Advancing to Next Day!");
                if (ShiftManager.Instance != null) ShiftManager.Instance.AdvanceToNextDay();
                else GameManager.Instance.StartNewDayBriefing();
            }
            else if (keyboard.uKey.wasPressedThisFrame)
            {
                Debug.Log("<color=red>[DEBUG]</color> U pressed -> Resetting Career to Day 1 (0 orders) and clearing upgrades!");
                if (ShiftManager.Instance != null) ShiftManager.Instance.ResetCareer();
                if (UpgradeManager.Instance != null) UpgradeManager.Instance.ResetUpgrades();
            }
        }
#endif
    }
}

