using RewardEngine.Application;
using RewardEngine.Domain;

namespace RewardEngine.UnitTests;

public sealed class CashbackCalculatorTests
{
    private readonly CashbackCalculator _calculator = new();
    [Fact] public void Calculates_regular_cashback() => Assert.Equal(250m, _calculator.Calculate(5000m, Rule(5m, 3000m), 0m));
    [Fact] public void Caps_cashback_at_remaining_monthly_limit() => Assert.Equal(100m, _calculator.Calculate(5000m, Rule(5m, 3000m), 2900m));
    [Fact] public void Returns_zero_when_limit_is_exhausted() => Assert.Equal(0m, _calculator.Calculate(5000m, Rule(5m, 3000m), 3000m));
    [Fact] public void Returns_zero_when_rule_is_missing() => Assert.Equal(0m, _calculator.Calculate(5000m, null, 0m));
    [Fact] public void Returns_zero_when_rule_is_inactive() => Assert.Equal(0m, _calculator.Calculate(5000m, Rule(5m, 3000m, false), 0m));
    [Theory, InlineData(0), InlineData(-1)] public void Rejects_non_positive_amount(decimal amount) => Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(amount, Rule(5m, 3000m), 0m));
    [Fact] public void Rounds_money_away_from_zero_to_two_decimals() => Assert.Equal(0.01m, _calculator.Calculate(0.10m, Rule(5m, 100m), 0m));
    private static CashbackRule Rule(decimal percentage, decimal limit, bool active = true) => new() { Category = TransactionCategory.Restaurants, Percentage = percentage, MonthlyLimit = limit, IsActive = active };
}
