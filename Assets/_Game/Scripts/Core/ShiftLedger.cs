using System;

namespace DoomDriver.Core
{
    /// <summary>
    /// Immutable end-of-day financial report shown by ShiftSummaryUI.
    /// All amounts are positive magnitudes; NetBankChange is the signed wallet delta for the day.
    /// </summary>
    public sealed class ShiftReport
    {
        public int Day { get; }
        public int DeliveriesCompleted { get; }
        public int DeliveriesFailed { get; }
        public decimal GrossEarnings { get; }
        public decimal SpeedTips { get; }
        public decimal DamageRepairs { get; }
        public decimal MissedTurnPenalties { get; }
        public decimal FailureFees { get; }
        public decimal BillsDue { get; }
        public decimal BillsPaid { get; }
        public decimal UnpaidBills => Math.Max(0m, BillsDue - BillsPaid);
        public decimal NetBankChange { get; }

        public ShiftReport(int day, int deliveriesCompleted, int deliveriesFailed,
            decimal grossEarnings, decimal speedTips, decimal damageRepairs, decimal missedTurnPenalties,
            decimal failureFees, decimal billsDue, decimal billsPaid, decimal netBankChange)
        {
            Day = day;
            DeliveriesCompleted = deliveriesCompleted;
            DeliveriesFailed = deliveriesFailed;
            GrossEarnings = Money(grossEarnings);
            SpeedTips = Money(speedTips);
            DamageRepairs = Money(damageRepairs);
            MissedTurnPenalties = Money(missedTurnPenalties);
            FailureFees = Money(failureFees);
            BillsDue = Money(billsDue);
            BillsPaid = Money(billsPaid);
            NetBankChange = Math.Round(netBankChange, 2, MidpointRounding.AwayFromZero);
        }

        private static decimal Money(decimal value) => Math.Round(Math.Max(0m, value), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Immutable running tally of one working shift. Every Record* call returns a new ledger.
    /// Pure C# so the accounting can be unit-tested without a scene.
    /// </summary>
    public sealed class ShiftLedger
    {
        public static readonly ShiftLedger Empty = new ShiftLedger(0, 0, 0m, 0m, 0m, 0m, 0m);

        public int DeliveriesCompleted { get; }
        public int DeliveriesFailed { get; }
        public decimal GrossEarnings { get; }
        public decimal SpeedTips { get; }
        public decimal DamageRepairs { get; }
        public decimal MissedTurnPenalties { get; }
        public decimal FailureFees { get; }

        private ShiftLedger(int completed, int failed, decimal gross, decimal tips,
            decimal repairs, decimal missedTurns, decimal failureFees)
        {
            DeliveriesCompleted = completed;
            DeliveriesFailed = failed;
            GrossEarnings = gross;
            SpeedTips = tips;
            DamageRepairs = repairs;
            MissedTurnPenalties = missedTurns;
            FailureFees = failureFees;
        }

        public ShiftLedger RecordDelivery(DeliveryResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            return new ShiftLedger(
                DeliveriesCompleted + 1,
                DeliveriesFailed,
                GrossEarnings + result.BaseFee,
                SpeedTips + result.TimeTip,
                DamageRepairs + result.DamagePenalty,
                MissedTurnPenalties + result.MissedTurnPenalty,
                FailureFees);
        }

        public ShiftLedger RecordFailure(decimal fees)
        {
            return new ShiftLedger(
                DeliveriesCompleted,
                DeliveriesFailed + 1,
                GrossEarnings,
                SpeedTips,
                DamageRepairs,
                MissedTurnPenalties,
                FailureFees + Math.Max(0m, fees));
        }

        public ShiftReport BuildReport(int day, decimal billsDue, decimal billsPaid, decimal startBalance, decimal endBalance)
        {
            return new ShiftReport(day, DeliveriesCompleted, DeliveriesFailed, GrossEarnings, SpeedTips,
                DamageRepairs, MissedTurnPenalties, FailureFees, billsDue, billsPaid, endBalance - startBalance);
        }
    }
}
