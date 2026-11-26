using UnityEngine;
using System.Collections;

namespace DoomDriver.Tests
{
    /// <summary>
    /// Test harness for exercising and verifying ShiftManager and GameState transitions.
    /// Can be added to a test GameObject to automatically or manually test the full working day lifecycle.
    /// </summary>
    public class ShiftManagerDemo : MonoBehaviour
    {
        [Header("Test Controls")]
        [SerializeField] private bool runSelfTestOnStart = false;

        [Header("Live Shift Metrics")]
        [SerializeField] private int dayIndex;
        [SerializeField] private int deliveredToday;
        [SerializeField] private int targetQuota;
        [SerializeField] private float shiftEarnings;
        [SerializeField] private bool quotaMet;
        [SerializeField] private bool shiftActive;
        [SerializeField] private GameState currentState;

        private void OnEnable()
        {
            ShiftManager.OnShiftStarted += HandleShiftStarted;
            ShiftManager.OnShiftCompleted += HandleShiftCompleted;
            ShiftManager.OnDayAdvanced += HandleDayAdvanced;
            ShiftManager.OnDeliveryCountChanged += HandleDeliveryCountChanged;
            ShiftManager.OnShiftEarningsChanged += HandleEarningsChanged;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            }
        }

        private void OnDisable()
        {
            ShiftManager.OnShiftStarted -= HandleShiftStarted;
            ShiftManager.OnShiftCompleted -= HandleShiftCompleted;
            ShiftManager.OnDayAdvanced -= HandleDayAdvanced;
            ShiftManager.OnDeliveryCountChanged -= HandleDeliveryCountChanged;
            ShiftManager.OnShiftEarningsChanged -= HandleEarningsChanged;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }
        }

        private void Start()
        {
            RefreshMetrics();

            if (runSelfTestOnStart)
            {
                StartCoroutine(RunVerificationRoutine());
            }
        }

        private void Update()
        {
            RefreshMetrics();
        }

        private void RefreshMetrics()
        {
            if (ShiftManager.Instance != null)
            {
                dayIndex = ShiftManager.Instance.CurrentDay;
                deliveredToday = ShiftManager.Instance.DeliveriesCompletedToday;
                targetQuota = ShiftManager.Instance.DailyQuota;
                shiftEarnings = ShiftManager.Instance.ShiftEarnings;
                quotaMet = ShiftManager.Instance.IsQuotaMet;
                shiftActive = ShiftManager.Instance.IsShiftActive;
            }

            if (GameManager.Instance != null)
            {
                currentState = GameManager.Instance.CurrentState;
            }
        }

        private void HandleShiftStarted()
        {
            Debug.Log("<color=green>[ShiftDemo]</color> Event: OnShiftStarted");
            RefreshMetrics();
        }

        private void HandleShiftCompleted()
        {
            Debug.Log("<color=yellow>[ShiftDemo]</color> Event: OnShiftCompleted (Quota Met!)");
            RefreshMetrics();
        }

        private void HandleDayAdvanced(int newDay)
        {
            Debug.Log($"<color=cyan>[ShiftDemo]</color> Event: OnDayAdvanced -> Day {newDay}");
            RefreshMetrics();
        }

        private void HandleDeliveryCountChanged(int delivered, int quota)
        {
            Debug.Log($"<color=magenta>[ShiftDemo]</color> Event: OnDeliveryCountChanged -> {delivered} / {quota}");
            RefreshMetrics();
        }

        private void HandleEarningsChanged(float earnings)
        {
            Debug.Log($"<color=lime>[ShiftDemo]</color> Event: OnShiftEarningsChanged -> €{earnings:F2}");
            RefreshMetrics();
        }

        private void HandleGameStateChanged(GameState state)
        {
            Debug.Log($"<color=orange>[ShiftDemo]</color> GameState -> {state}");
            RefreshMetrics();
        }

        /// <summary>
        /// Context menu action to simulate completing a delivery.
        /// </summary>
        [ContextMenu("Simulate Complete Delivery (€25)")]
        public void SimulateDelivery()
        {
            if (ShiftManager.Instance != null)
            {
                ShiftManager.Instance.RecordDeliveryCompleted(25m);
            }
        }

        /// <summary>
        /// Context menu action to simulate starting a shift.
        /// </summary>
        [ContextMenu("Simulate Start Shift")]
        public void SimulateStartShift()
        {
            if (ShiftManager.Instance != null)
            {
                ShiftManager.Instance.StartShift();
            }
        }

        /// <summary>
        /// Context menu action to end shift.
        /// </summary>
        [ContextMenu("Simulate End Shift")]
        public void SimulateEndShift()
        {
            if (ShiftManager.Instance != null)
            {
                ShiftManager.Instance.EndShift();
            }
        }

        /// <summary>
        /// Context menu action to advance to next day.
        /// </summary>
        [ContextMenu("Simulate Advance Next Day")]
        public void SimulateNextDay()
        {
            if (ShiftManager.Instance != null)
            {
                ShiftManager.Instance.AdvanceToNextDay();
            }
        }

        /// <summary>
        /// Automated verification sequence testing day progression, quota, overtime, and reset.
        /// </summary>
        public IEnumerator RunVerificationRoutine()
        {
            yield return null;
            Debug.Log("<color=yellow>[ShiftDemo] Starting Automated Self-Test...</color>");

            if (ShiftManager.Instance == null)
            {
                Debug.LogError("[ShiftDemo] Self-test failed: ShiftManager.Instance is null!");
                yield break;
            }

            // 1. Reset to clean slate
            ShiftManager.Instance.ResetCareer();
            yield return null;
            Assert(ShiftManager.Instance.CurrentDay == 1, "Day should be 1 after reset");
            Assert(ShiftManager.Instance.DeliveriesCompletedToday == 0, "Deliveries should be 0 after reset");

            // 2. Start Shift
            ShiftManager.Instance.StartShift();
            yield return null;
            Assert(ShiftManager.Instance.IsShiftActive, "Shift should be active");

            // 3. Deliver orders up to quota
            int quota = ShiftManager.Instance.DailyQuota;
            for (int i = 1; i <= quota; i++)
            {
                ShiftManager.Instance.RecordDeliveryCompleted(20m);
                yield return null;
                Assert(ShiftManager.Instance.DeliveriesCompletedToday == i, $"Delivered count should be {i}");
            }
            Assert(ShiftManager.Instance.IsQuotaMet, "Quota should be met");

            // 4. Overtime delivery
            ShiftManager.Instance.RecordDeliveryCompleted(30m);
            yield return null;
            Assert(ShiftManager.Instance.DeliveriesCompletedToday == quota + 1, "Overtime delivery should be allowed");

            // 5. Advance to Next Day
            ShiftManager.Instance.AdvanceToNextDay();
            yield return null;
            Assert(ShiftManager.Instance.CurrentDay == 2, "Day should advance to 2");
            Assert(ShiftManager.Instance.DeliveriesCompletedToday == 0, "Daily deliveries should reset to 0 for Day 2");
            Assert(ShiftManager.Instance.ShiftEarnings == 0f, "Shift earnings should reset to 0 for Day 2");

            Debug.Log("<color=green>[ShiftDemo] ALL SHIFT TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void Assert(bool condition, string message)
        {
            if (!condition)
            {
                Debug.LogError($"<color=red>[ShiftDemo ASSERTION FAILED]</color> {message}");
            }
            else
            {
                Debug.Log($"<color=green>[ShiftDemo PASS]</color> {message}");
            }
        }
    }
}
