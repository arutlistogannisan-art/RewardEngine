using Microsoft.EntityFrameworkCore;
using RewardEngine.Domain;
using RewardEngine.Infrastructure;

namespace RewardEngine.UnitTests;

public sealed class IdempotencyModelTests
{
    [Fact]
    public void Event_and_reward_have_database_idempotency_guards()
    {
        using var db = new RewardEngineDbContext(new DbContextOptionsBuilder<RewardEngineDbContext>().UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").Options);
        var processed = db.Model.FindEntityType(typeof(ProcessedEvent))!;
        Assert.Contains(processed.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == nameof(ProcessedEvent.EventId));
        var reward = db.Model.FindEntityType(typeof(CashbackReward))!;
        Assert.Contains(reward.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == nameof(CashbackReward.TransactionId));
    }
}
