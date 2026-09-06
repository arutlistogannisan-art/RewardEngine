namespace RewardEngine.Domain;

public enum TransactionCategory
{
    Other, Restaurants, Taxi, Fuel, Groceries, Entertainment
}
public enum TransactionStatus
{
    Created, Rewarded
}

public sealed class User
{
    public long Id
    {
        get; set;
    }
    public required string Name
    {
        get; set;
    }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<Transaction> Transactions { get; set; } = [];
}

public sealed class Transaction
{
    public long Id
    {
        get; set;
    }
    public long UserId
    {
        get; set;
    }
    public User User { get; set; } = null!;
    public decimal Amount
    {
        get; set;
    }
    public TransactionCategory Category
    {
        get; set;
    }
    public required string Merchant
    {
        get; set;
    }
    public TransactionStatus Status { get; set; } = TransactionStatus.Created;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public CashbackReward? Reward
    {
        get; set;
    }
}

public sealed class CashbackRule
{
    public long Id
    {
        get; set;
    }
    public TransactionCategory Category
    {
        get; set;
    }
    public decimal Percentage
    {
        get; set;
    }
    public decimal MonthlyLimit
    {
        get; set;
    }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CashbackReward
{
    public long Id
    {
        get; set;
    }
    public long TransactionId
    {
        get; set;
    }
    public Transaction Transaction { get; set; } = null!;
    public long UserId
    {
        get; set;
    }
    public User User { get; set; } = null!;
    public decimal Amount
    {
        get; set;
    }
    public decimal Percentage
    {
        get; set;
    }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ProcessedEvent
{
    public Guid EventId
    {
        get; set;
    }
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class OutboxMessage
{
    public Guid Id
    {
        get; set;
    }
    public required string EventType
    {
        get; set;
    }
    public required string Payload
    {
        get; set;
    }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt
    {
        get; set;
    }
    public int RetryCount
    {
        get; set;
    }
    public DateTimeOffset? NextAttemptAt
    {
        get; set;
    }
    public string? LastError
    {
        get; set;
    }
}

public sealed record TransactionCreatedEvent(
    Guid EventId, long TransactionId, long UserId, decimal Amount,
    TransactionCategory Category, DateTimeOffset CreatedAt);
