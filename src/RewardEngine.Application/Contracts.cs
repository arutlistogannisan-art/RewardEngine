using System.ComponentModel.DataAnnotations;
using RewardEngine.Domain;

namespace RewardEngine.Application;

public sealed record CreateUserRequest([Required, StringLength(200, MinimumLength = 1)] string Name);
public sealed record UserDto(long Id, string Name, DateTimeOffset CreatedAt);
public sealed record CreateTransactionRequest(
    [Range(1, long.MaxValue)] long UserId,
    [Range(typeof(decimal), "0.01", "999999999999", ParseLimitsInInvariantCulture = true)] decimal Amount,
    [EnumDataType(typeof(TransactionCategory))] TransactionCategory Category,
    [Required, StringLength(300, MinimumLength = 1)] string Merchant);
public sealed record TransactionDto(long Id, long UserId, decimal Amount, TransactionCategory Category, string Merchant, TransactionStatus Status, DateTimeOffset CreatedAt);
public sealed record CreateCashbackRuleRequest([EnumDataType(typeof(TransactionCategory))] TransactionCategory Category, [Range(typeof(decimal), "0", "100")] decimal Percentage, [Range(typeof(decimal), "0", "999999999999")] decimal MonthlyLimit);
public sealed record UpdateCashbackRuleRequest([Range(typeof(decimal), "0", "100")] decimal Percentage, [Range(typeof(decimal), "0", "999999999999")] decimal MonthlyLimit, bool IsActive);
public sealed record CashbackRuleDto(long Id, TransactionCategory Category, decimal Percentage, decimal MonthlyLimit, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record CashbackSummaryDto(long UserId, decimal TotalCashback);
public sealed record CashbackHistoryItemDto(long RewardId, long TransactionId, decimal TransactionAmount, TransactionCategory Category, string Merchant, decimal CashbackAmount, decimal Percentage, DateTimeOffset CreatedAt);

public sealed class AppException(string code, string message, int statusCode) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public interface IRewardEngineDbContext
{
    IQueryable<User> Users
    {
        get;
    }
    IQueryable<Transaction> Transactions
    {
        get;
    }
    IQueryable<CashbackRule> CashbackRules
    {
        get;
    }
    IQueryable<CashbackReward> CashbackRewards
    {
        get;
    }
    IQueryable<ProcessedEvent> ProcessedEvents
    {
        get;
    }
    IQueryable<OutboxMessage> OutboxMessages
    {
        get;
    }
    void Add<TEntity>(TEntity entity) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken);
}

public interface ICashbackRuleCache
{
    Task<CashbackRule?> GetAsync(TransactionCategory category, CancellationToken cancellationToken);
    Task SetAsync(CashbackRule rule, CancellationToken cancellationToken);
    Task InvalidateAsync(TransactionCategory category, CancellationToken cancellationToken);
}

public interface IEventPublisher
{
    Task PublishAsync(TransactionCreatedEvent message, CancellationToken cancellationToken);
}
