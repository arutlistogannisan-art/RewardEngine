using RewardEngine.Domain;

namespace RewardEngine.Application;

public sealed class CashbackCalculator
{
    public decimal Calculate(decimal amount, CashbackRule? rule, decimal alreadyEarned)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Transaction amount must be positive.");
        if (alreadyEarned < 0)
            throw new ArgumentOutOfRangeException(nameof(alreadyEarned));
        if (rule is null || !rule.IsActive)
            return 0m;

        var available = Math.Max(0m, rule.MonthlyLimit - alreadyEarned);
        var calculated = Math.Round(amount * rule.Percentage / 100m, 2, MidpointRounding.AwayFromZero);
        return Math.Min(calculated, available);
    }
}
