using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardEngine.Application;
using RewardEngine.Domain;

namespace RewardEngine.Infrastructure;

// Database-specific coordination belongs here, not in the RabbitMQ transport.
public sealed class RewardProcessor(
    RewardEngineDbContext db,
    ICashbackRuleCache cache,
    CashbackCalculator calculator,
    ILogger<RewardProcessor> logger)
{
    public async Task ProcessAsync(TransactionCreatedEvent message, CancellationToken ct)
    {
        var purchase = await db.TransactionSet.SingleOrDefaultAsync(x => x.Id == message.TransactionId, ct)
            ?? throw new InvalidDataException("Event references a missing transaction.");
        if (message.EventId == Guid.Empty || message.UserId != purchase.UserId ||
            message.Amount != purchase.Amount || message.Category != purchase.Category)
            throw new InvalidDataException("Event does not match the persisted transaction.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // One transaction-scoped lock per user. Concurrent workers cannot spend
        // the same remaining monthly limit. PostgreSQL releases it on rollback too.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({purchase.UserId})", ct);
        if (await db.ProcessedEventSet.AnyAsync(x => x.EventId == message.EventId, ct))
        {
            logger.LogInformation("Duplicate event {EventId} ignored", message.EventId);
            return;
        }

        if (!await db.CashbackRewardSet.AnyAsync(x => x.TransactionId == purchase.Id, ct))
        {
            var rule = await cache.GetAsync(purchase.Category, ct);
            if (rule is null)
            {
                rule = await db.CashbackRuleSet.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Category == purchase.Category && x.IsActive, ct);
                if (rule is not null)
                    await cache.SetAsync(rule, ct);
            }

            var date = purchase.CreatedAt.ToUniversalTime();
            var start = new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var end = start.AddMonths(1);
            var earned = await db.CashbackRewardSet
                .Where(x => x.UserId == purchase.UserId && x.Transaction.Category == purchase.Category
                    && x.Transaction.CreatedAt >= start && x.Transaction.CreatedAt < end)
                .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
            var amount = calculator.Calculate(purchase.Amount, rule, earned);
            db.CashbackRewardSet.Add(new CashbackReward
            {
                TransactionId = purchase.Id,
                UserId = purchase.UserId,
                Amount = amount,
                Percentage = rule is { IsActive: true } ? rule.Percentage : 0m
            });
            purchase.Status = TransactionStatus.Rewarded;
            logger.LogInformation("Calculated cashback {Amount} for transaction {TransactionId}", amount, purchase.Id);
        }

        db.ProcessedEventSet.Add(new ProcessedEvent { EventId = message.EventId });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
