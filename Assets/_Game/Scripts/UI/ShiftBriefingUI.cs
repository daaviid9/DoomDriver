using TMPro;
using UnityEngine;
using DoomDriver.Core;
using DoomDriver.Environment;
using DoomDriver.Phone;

namespace DoomDriver.UI
{
    /// <summary>
    /// Morning dispatcher briefing on the phone at the start of each working day:
    /// procedural dispatcher message, today's delivery quota and the bills due tonight.
    /// "Start Shift" opens the shift and jumps to the Courier app.
    /// </summary>
    public sealed class ShiftBriefingUI : ShiftOverlayBase
    {
        private TMP_Text headerText;
        private TMP_Text titleText;
        private TMP_Text messageText;
        private TMP_Text quotaText;
        private TMP_Text billsText;
        private TMP_Text overtimeText;
        private TMP_Text walletText;

        protected override GameState VisibleState => GameState.DayBriefing;
        protected override string ObjectName => "ShiftBriefingPanel";
        protected override string ButtonLabel => "START SHIFT";

        protected override void BuildContent(RectTransform content)
        {
            (headerText, titleText) = CreateHeader(content);

            // Dispatcher message as a light-blue chat bubble (same tint as the order card's info panel).
            var messageCard = CreateCard(content, InfoCardColor);
            var fromLabel = CreateText(messageCard, SMALL_FONT_SIZE, FontStyles.Bold, TextAlignmentOptions.Left, HighlightColor);
            fromLabel.text = "MESSAGE FROM DISPATCH";
            messageText = CreateText(messageCard, BODY_FONT_SIZE, FontStyles.Italic, TextAlignmentOptions.Left, TextColor);

            var todayCard = CreateCard(content);
            quotaText = CreateRow(todayCard, "Today's quota");
            billsText = CreateRow(todayCard, "Bills due tonight");
            overtimeText = CreateText(todayCard, SMALL_FONT_SIZE, FontStyles.Italic, TextAlignmentOptions.Right, PositiveColor);
            CreateDivider(todayCard);
            walletText = CreateRow(todayCard, "Wallet");
        }

        protected override void Refresh(ShiftManager shift)
        {
            var briefing = DailyShiftBriefing.Instance;
            int day = shift.CurrentDay;
            string dayName = DailyShiftBriefing.DayNameFor(day);
            WeatherCondition weather = CurrentWeather(briefing);

            string message = DispatcherMessageGenerator.Build(day, dayName, weather);
            if (briefing != null) briefing.SetDispatcherBriefing(message);

            headerText.text = $"Day {day}  •  Dispatch";
            titleText.text = $"{dayName} Briefing";
            messageText.text = $"\"{message}\"";
            quotaText.text = $"{shift.DailyQuota} deliveries";
            billsText.text = FormatMoney((decimal)shift.DailyBills);
            billsText.color = NegativeColor;

            bool hasOvertime = shift.AllowOvertime && shift.OvertimeBonusPercent > 0;
            overtimeText.gameObject.SetActive(hasOvertime);
            if (hasOvertime) overtimeText.text = $"Overtime after quota: +{shift.OvertimeBonusPercent}% speed tip";

            walletText.text = WalletManager.Instance != null ? FormatMoney((decimal)WalletManager.Instance.Balance) : "-";
        }

        protected override void HandleButtonPressed(ShiftManager shift)
        {
            // StartShift itself moves GameManager DayBriefing -> OrderSelection, so the following
            // OpenScreen(CourierApp) no longer hits PhoneOSController's "start shift on open" path (no double start).
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.DayBriefing) return;
            if (!shift.IsShiftActive) shift.StartShift();
            else GameManager.Instance.TransitionFromBriefingToShift();

            if (PhoneOS != null) PhoneOS.OpenScreen(PhoneScreen.CourierApp);
        }

        private static WeatherCondition CurrentWeather(DailyShiftBriefing briefing)
        {
            if (WeatherManager.Instance != null) return WeatherManager.Instance.CurrentCondition;
            return briefing != null ? briefing.CurrentWeatherCondition : WeatherCondition.Clear;
        }
    }
}
