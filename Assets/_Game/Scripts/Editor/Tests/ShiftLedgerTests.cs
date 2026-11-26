using NUnit.Framework;
using DoomDriver.Core;
using DoomDriver.Environment;

/// <summary>
/// EditMode tests for the end-of-day accounting and the dispatcher message (Window > General > Test Runner > EditMode).
/// </summary>
public class ShiftLedgerTests
{
    [Test]
    public void RecordDelivery_SumsLinesAndCountsDelivery_WithoutChangingOriginal()
    {
        var empty = ShiftLedger.Empty;

        var ledger = empty
            .RecordDelivery(new DeliveryResult(12m, 5m, 1.5m, 2m))
            .RecordDelivery(new DeliveryResult(8m, 0m, 0.5m, 0m));

        Assert.AreEqual(2, ledger.DeliveriesCompleted);
        Assert.AreEqual(20m, ledger.GrossEarnings);
        Assert.AreEqual(5m, ledger.SpeedTips);
        Assert.AreEqual(2m, ledger.DamageRepairs);
        Assert.AreEqual(2m, ledger.MissedTurnPenalties);
        Assert.AreEqual(0, empty.DeliveriesCompleted);
    }

    [Test]
    public void RecordFailure_AddsFeesAndCountsFailure_NotTowardQuota()
    {
        var ledger = ShiftLedger.Empty.RecordFailure(30m);

        Assert.AreEqual(0, ledger.DeliveriesCompleted);
        Assert.AreEqual(1, ledger.DeliveriesFailed);
        Assert.AreEqual(30m, ledger.FailureFees);
    }

    [Test]
    public void BuildReport_PartialBillPayment_ReportsUnpaidRemainderAndSignedNetChange()
    {
        var ledger = ShiftLedger.Empty.RecordDelivery(new DeliveryResult(10m, 2m, 0m, 0m));

        var report = ledger.BuildReport(day: 2, billsDue: 45m, billsPaid: 30m, startBalance: 40m, endBalance: 22m);

        Assert.AreEqual(2, report.Day);
        Assert.AreEqual(15m, report.UnpaidBills);
        Assert.AreEqual(-18m, report.NetBankChange);
    }

    [Test]
    public void DispatcherMessage_IsDeterministicPerDay_AndKeepsTheCurrentWeather()
    {
        foreach (WeatherCondition weather in System.Enum.GetValues(typeof(WeatherCondition)))
        {
            for (int day = 1; day <= 14; day++)
            {
                string message = DispatcherMessageGenerator.Build(day, "Tuesday", weather);

                Assert.AreEqual(message, DispatcherMessageGenerator.Build(day, "Tuesday", weather));
                StringAssert.Contains("Tuesday: ", message);
                Assert.AreEqual(weather, DoomDriver.Environment.WeatherManager.ParseConditionFromBriefing(message), message);
            }
        }
    }
}
