using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DoomDriver.Phone
{
    /// <summary>
    /// Pure presentation (View) controller for the 4-state Workday / Shift panel on the Phone OS Home Screen.
    /// Switches between Start Shift, Ongoing, Bonus (Overtime), and End Shift panels based on ShiftManager & GameManager state.
    /// Reuses shared text arrays so identical fields across multiple panels can be bound cleanly in Inspector.
    /// </summary>
    public class ShiftPanelUI : MonoBehaviour
    {
        [Header("4 Shift State Panels")]
        [SerializeField] private GameObject startShiftPanel;
        [SerializeField] private GameObject ongoingPanel;
        [SerializeField] private GameObject bonusPanel;
        [SerializeField] private GameObject endShiftPanel;

        [Header("Shared Elements (Reused Across Panels)")]
        [Tooltip("Day labels on any/all panels (formatted as 'DAY x')")]
        [SerializeField] private TMP_Text[] dayTexts;

        [Tooltip("Shift earnings on Ongoing, Bonus, and End Shift panels (formatted as '+ x.xx€')")]
        [SerializeField] private TMP_Text[] shiftEarningsTexts;

        [Tooltip("Orders progress on Ongoing and Bonus panels (formatted as 'x/x ORDERS')")]
        [SerializeField] private TMP_Text[] ordersProgressTexts;

        [Tooltip("Daily bills on Start Shift and End Shift panels (formatted as '- x.xx€')")]
        [SerializeField] private TMP_Text[] dailyBillsTexts;

        [Tooltip("Overtime bonus on Start Shift and Bonus panels (formatted as '+x%')")]
        [SerializeField] private TMP_Text[] overtimeBonusTexts;

        [Header("Start Shift Panel")]
        [Tooltip("Target deliveries label (formatted as 'X DELIVERIES')")]
        [SerializeField] private TMP_Text startShiftDeliveriesText;
        [SerializeField] private Button startShiftButton;

        [Header("Ongoing Panel")]
        [Tooltip("Progress slider on the Ongoing Shift panel (maxValue = dailyQuota, value = deliveriesCompletedToday)")]
        [SerializeField] private Slider ongoingProgressSlider;

        [Header("Ongoing & Bonus Panels")]
        [Tooltip("Buttons on Ongoing and Bonus (Next Order) panels that open the Courier App")]
        [SerializeField] private Button[] openCourierAppButtons;

        [Header("Bonus (Overtime) Panel")]
        [Tooltip("Clock-out / End Shift button on the Bonus (Overtime) panel")]
        [SerializeField] private Button clockOutButton;

        [Header("End Shift Panel")]
        [Tooltip("Completed vs quota orders (formatted as 'x/y')")]
        [SerializeField] private TMP_Text endShiftOrdersText;

        [Tooltip("Net income after daily bills (formatted as '+x.xx€ NET' or '-x.xx€ NET')")]
        [SerializeField] private TMP_Text netIncomeText;

        [SerializeField] private Button advanceDayButton;
        [SerializeField] private TMP_Text advanceDayButtonText;

        private void Awake()
        {
            BindButtons();
        }

        private void OnEnable()
        {
            ShiftManager.OnShiftStarted += HandleShiftStateChanged;
            ShiftManager.OnDeliveryCountChanged += HandleDeliveryCountChanged;
            ShiftManager.OnShiftEarningsChanged += HandleShiftEarningsChanged;
            ShiftManager.OnDayAdvanced += HandleDayAdvanced;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            }

            Refresh();
        }

        private void OnDisable()
        {
            ShiftManager.OnShiftStarted -= HandleShiftStateChanged;
            ShiftManager.OnDeliveryCountChanged -= HandleDeliveryCountChanged;
            ShiftManager.OnShiftEarningsChanged -= HandleShiftEarningsChanged;
            ShiftManager.OnDayAdvanced -= HandleDayAdvanced;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }
        }

        private void Start()
        {
            Refresh();
        }

        private void HandleShiftStateChanged() => Refresh();
        private void HandleDeliveryCountChanged(int delivered, int quota) => Refresh();
        private void HandleShiftEarningsChanged(float earnings) => Refresh();
        private void HandleDayAdvanced(int newDay) => Refresh();
        private void HandleGameStateChanged(GameState state) => Refresh();

        private void BindButtons()
        {
            if (startShiftButton != null)
            {
                startShiftButton.onClick.RemoveListener(OnStartShiftClicked);
                startShiftButton.onClick.AddListener(OnStartShiftClicked);
            }

            if (openCourierAppButtons != null)
            {
                foreach (var btn in openCourierAppButtons)
                {
                    if (btn == null) continue;
                    btn.onClick.RemoveListener(OnOpenCourierAppClicked);
                    btn.onClick.AddListener(OnOpenCourierAppClicked);
                }
            }

            if (clockOutButton != null)
            {
                clockOutButton.onClick.RemoveListener(OnClockOutClicked);
                clockOutButton.onClick.AddListener(OnClockOutClicked);
            }

            if (advanceDayButton != null)
            {
                advanceDayButton.onClick.RemoveListener(OnAdvanceDayClicked);
                advanceDayButton.onClick.AddListener(OnAdvanceDayClicked);
            }
        }

        private void OnStartShiftClicked()
        {
            if (ShiftManager.Instance != null)
            {
                ShiftManager.Instance.StartShift();
            }
            Refresh();
        }

        private void OnOpenCourierAppClicked()
        {
            if (PhoneOSController.Instance != null)
            {
                PhoneOSController.Instance.OpenScreen(PhoneScreen.CourierApp);
            }
        }

        private void OnClockOutClicked()
        {
            if (ShiftManager.Instance != null)
            {
                ShiftManager.Instance.EndShift();
            }
            Refresh();
        }

        private void OnAdvanceDayClicked()
        {
            if (ShiftManager.Instance != null)
            {
                ShiftManager.Instance.AdvanceToNextDay();
            }
            Refresh();
        }

        /// <summary>
        /// Refreshes active panel visibility and updates all shared & specific UI text fields.
        /// </summary>
        public void Refresh()
        {
            var shift = ShiftManager.Instance;
            int day = shift != null ? shift.CurrentDay : 1;
            int delivered = shift != null ? shift.DeliveriesCompletedToday : 0;
            int quota = shift != null ? shift.DailyQuota : 3;
            float earnings = shift != null ? shift.ShiftEarnings : 0f;
            float bills = shift != null ? shift.DailyBills : 25f;
            int bonusPercent = shift != null ? shift.OvertimeBonusPercent : 25;
            bool isShiftActive = shift != null && shift.IsShiftActive;
            bool isQuotaMet = shift != null && shift.IsQuotaMet;
            bool isSummary = GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DaySummary;

            // 1. Toggle active panel
            SetPanelActive(startShiftPanel, !isSummary && !isShiftActive);
            SetPanelActive(ongoingPanel, !isSummary && isShiftActive && !isQuotaMet);
            SetPanelActive(bonusPanel, !isSummary && isShiftActive && isQuotaMet);
            SetPanelActive(endShiftPanel, isSummary);

            // 2. Update shared fields
            SetTexts(dayTexts, $"DAY {day}");
            SetTexts(shiftEarningsTexts, $"+ {earnings.ToString("F2", CultureInfo.InvariantCulture)}€");
            SetTexts(ordersProgressTexts, $"{delivered}/{quota} ORDERS");
            SetTexts(dailyBillsTexts, $"- {bills.ToString("F2", CultureInfo.InvariantCulture)}€");
            SetTexts(overtimeBonusTexts, $"+{bonusPercent}%");

            // 3. Update Start Shift & Ongoing specific fields
            if (startShiftDeliveriesText != null)
            {
                startShiftDeliveriesText.text = $"{quota} DELIVERIES";
            }

            if (ongoingProgressSlider != null)
            {
                int safeQuota = Mathf.Max(1, quota);
                ongoingProgressSlider.minValue = 0f;
                ongoingProgressSlider.maxValue = safeQuota;
                ongoingProgressSlider.value = Mathf.Clamp(delivered, 0, safeQuota);
            }

            // 4. Update End Shift specific fields
            if (endShiftOrdersText != null)
            {
                endShiftOrdersText.text = $"{delivered}/{quota}";
            }

            if (netIncomeText != null)
            {
                float net = shift != null ? shift.NetShiftIncome : -bills;
                string sign = net >= 0f ? "+" : "-";
                netIncomeText.text = $"{sign}{Mathf.Abs(net).ToString("F2", CultureInfo.InvariantCulture)}€ NET";
            }

            if (advanceDayButtonText != null)
            {
                advanceDayButtonText.text = $"Advance to Day {day + 1}";
            }
        }

        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null && panel.activeSelf != active)
            {
                panel.SetActive(active);
            }
        }

        private static void SetTexts(TMP_Text[] texts, string value)
        {
            if (texts == null) return;
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null)
                {
                    texts[i].text = value;
                }
            }
        }
    }
}
