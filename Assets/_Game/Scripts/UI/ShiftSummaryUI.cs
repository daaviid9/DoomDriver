using TMPro;
using UnityEngine;
using DoomDriver.Core;

namespace DoomDriver.UI
{
    /// <summary>
    /// End-of-day financial report shown in GameState.DaySummary (after ShiftManager.EndShift deducted the bills):
    /// gross earnings, speed tips, damage repairs, penalties, bills deducted and net bank change.
    /// "Clock Out &amp; Sleep" calls ShiftManager.AdvanceToNextDay, which returns to the next day's DayBriefing.
    /// The line-item breakdown is a presentation tally kept here from GameManager delivery events.
    /// shortcut: the tally is in memory only; after a restart mid-shift the report shows upstream counts but
    /// zeroed line items. Persist ShiftLedger (or move it into ShiftManager) if that matters.
    /// </summary>
    public sealed class ShiftSummaryUI : ShiftOverlayBase
    {
        private TMP_Text headerText;
        private TMP_Text titleText;
        private TMP_Text deliveriesText;
        private TMP_Text grossText;
        private TMP_Text tipsText;
        private TMP_Text repairsText;
        private TMP_Text missedTurnsText;
        private TMP_Text failureFeesText;
        private TMP_Text billsText;
        private TMP_Text unpaidText;
        private TMP_Text netText;
        private TMP_Text walletText;

        private ShiftLedger ledger = ShiftLedger.Empty;
        private decimal shiftStartBalance;
        private GameManager boundManager;

        protected override GameState VisibleState => GameState.DaySummary;
        protected override string ObjectName => "ShiftSummaryPanel";
        protected override string ButtonLabel => "CLOCK OUT & SLEEP";

        protected override void BuildContent(RectTransform content)
        {
            (headerText, titleText) = CreateHeader(content);
            deliveriesText = CreateText(content, SMALL_FONT_SIZE, FontStyles.Bold, TextAlignmentOptions.Left, HighlightColor);

            var breakdownCard = CreateCard(content);
            grossText = CreateRow(breakdownCard, "Gross delivery earnings");
            tipsText = CreateRow(breakdownCard, "Speed tips");
            repairsText = CreateRow(breakdownCard, "Damage repairs");
            missedTurnsText = CreateRow(breakdownCard, "Missed turn penalties");
            failureFeesText = CreateRow(breakdownCard, "Failed order fees");
            CreateDivider(breakdownCard);
            billsText = CreateRow(breakdownCard, "Daily bills deducted");
            unpaidText = CreateText(breakdownCard, SMALL_FONT_SIZE, FontStyles.Italic, TextAlignmentOptions.Right, NegativeColor);

            // Bottom line in the light-blue info tint so the result stands out.
            var totalCard = CreateCard(content, InfoCardColor);
            netText = CreateRow(totalCard, "Net bank change");
            walletText = CreateRow(totalCard, "Wallet balance");
        }

        protected override void Start()
        {
            base.Start();

            ShiftManager.OnShiftStarted += ResetTally;
            ShiftManager.OnDayAdvanced += HandleDayAdvanced;
            boundManager = GameManager.Instance;
            if (boundManager != null)
            {
                boundManager.OnDeliveryCompleted += HandleDeliveryCompleted;
                boundManager.OnDeliveryFailed += HandleDeliveryFailed;
            }

            // A shift restored from PlayerPrefs never raises OnShiftStarted: start the tally from the current wallet.
            ResetTally();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            ShiftManager.OnShiftStarted -= ResetTally;
            ShiftManager.OnDayAdvanced -= HandleDayAdvanced;
            if (boundManager != null)
            {
                boundManager.OnDeliveryCompleted -= HandleDeliveryCompleted;
                boundManager.OnDeliveryFailed -= HandleDeliveryFailed;
            }
        }

        private void ResetTally()
        {
            ledger = ShiftLedger.Empty;
            shiftStartBalance = CurrentBalance();
        }

        private void HandleDayAdvanced(int day) => ResetTally();

        private void HandleDeliveryCompleted()
        {
            var result = boundManager != null ? boundManager.CompletedDelivery : null;
            if (result != null) ledger = ledger.RecordDelivery(result);
        }

        private void HandleDeliveryFailed()
        {
            // Read the settled result once per failure (DeliveryPenaltyManager.ProcessFailure runs before this event).
            var penalty = DoomDriver.Economy.DeliveryPenaltyManager.Instance != null
                ? DoomDriver.Economy.DeliveryPenaltyManager.Instance.LatestResult
                : null;
            ledger = ledger.RecordFailure(penalty != null ? (decimal)penalty.TotalPenalty : 0m);
        }

        private static decimal CurrentBalance()
        {
            return WalletManager.Instance != null
                ? System.Math.Round((decimal)WalletManager.Instance.Balance, 2, System.MidpointRounding.AwayFromZero)
                : 0m;
        }

        protected override void Refresh(ShiftManager shift)
        {
            ShiftReport report = ledger.BuildReport(shift.CurrentDay, (decimal)shift.DailyBills,
                (decimal)shift.LastBillsPaid, shiftStartBalance, CurrentBalance());

            string dayName = DoomDriver.Core.DailyShiftBriefing.DayNameFor(report.Day);
            headerText.text = $"End of Day {report.Day}  •  {dayName}";
            titleText.text = "Financial Report";
            // Delivered count comes from ShiftManager (source of truth, survives restarts); failures from the tally.
            int delivered = shift.DeliveriesCompletedToday;
            deliveriesText.text = report.DeliveriesFailed > 0
                ? $"{delivered}/{shift.DailyQuota} delivered, {report.DeliveriesFailed} failed"
                : $"{delivered}/{shift.DailyQuota} delivered";

            SetAmount(grossText, report.GrossEarnings);
            SetAmount(tipsText, report.SpeedTips);
            SetAmount(repairsText, -report.DamageRepairs);
            SetAmount(missedTurnsText, -report.MissedTurnPenalties);
            SetAmount(failureFeesText, -report.FailureFees);
            SetAmount(billsText, -report.BillsPaid);
            SetAmount(netText, report.NetBankChange);

            bool hasUnpaid = report.UnpaidBills > 0m;
            unpaidText.gameObject.SetActive(hasUnpaid);
            if (hasUnpaid) unpaidText.text = $"UNPAID {FormatMoney(report.UnpaidBills)} - wallet could not cover tonight's bills";

            walletText.text = WalletManager.Instance != null ? FormatMoney((decimal)WalletManager.Instance.Balance) : "-";
        }

        protected override void HandleButtonPressed(ShiftManager shift)
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.DaySummary) return;
            shift.AdvanceToNextDay();
        }

        private static void SetAmount(TMP_Text text, decimal signedAmount)
        {
            text.text = FormatSigned(signedAmount);
            text.color = signedAmount > 0m ? PositiveColor : signedAmount < 0m ? NegativeColor : MutedColor;
        }
    }
}
