using System;

/// <summary>Immutable, cent-rounded accounting input/output. Backend supplies final line amounts.</summary>
public sealed class DeliveryResult
{
    public decimal BaseFee { get; }
    public decimal TimeTip { get; }
    public decimal DamagePenalty { get; }
    public decimal MissedTurnPenalty { get; }
    // Provisional policy: successful deliveries never create wallet debt.
    public decimal NetEarnings => Math.Max(0m, BaseFee + TimeTip - DamagePenalty - MissedTurnPenalty);
    public bool IsPlaceholder { get; }
    public float ElapsedSeconds { get; }
    public int DamageHp { get; }

    public DeliveryResult(decimal baseFee, decimal timeTip, decimal damagePenalty,
        decimal missedTurnPenalty, bool isPlaceholder = false, float elapsedSeconds = 0, int damageHp = 0)
    {
        BaseFee = Money(baseFee);
        TimeTip = Money(timeTip);
        DamagePenalty = Money(damagePenalty);
        MissedTurnPenalty = Money(missedTurnPenalty);
        IsPlaceholder = isPlaceholder;
        ElapsedSeconds = elapsedSeconds;
        DamageHp = damageHp;
    }

    private static decimal Money(decimal value)
    {
        if (value < 0m || value > 1000000m) throw new ArgumentOutOfRangeException(nameof(value));
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
