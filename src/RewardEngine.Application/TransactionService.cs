using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardEngine.Domain;

namespace RewardEngine.Application;

public sealed class TransactionService(IRewardEngineDbContext db, ILogger<TransactionService> logger)
{
    public async Task<TransactionDto> CreateAsync(CreateTransactionRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Category) || decimal.Round(request.Amount, 2) != request.Amount)
            throw new AppException("ValidationFailed", "Use a valid category and at most two decimal places for money.", 400);
        if (!await db.Users.AnyAsync(x => x.Id == request.UserId, ct))
            throw new AppException("UserNotFound", $"User with id {request.UserId} was not found.", 404);
        var merchant = request.Merchant.Trim();
        if (request.Amount <= 0 || merchant.Length == 0)
            throw new AppException("ValidationFailed", "Amount must be positive and merchant must not be empty.", 400);
        var transaction = new Transaction { UserId = request.UserId, Amount = request.Amount, Category = request.Category, Merchant = merchant };
        var eventId = Guid.NewGuid();
        await db.ExecuteInTransactionAsync(async token =>
        {
            db.Add(transaction);
            await db.SaveChangesAsync(token);
            var message = new TransactionCreatedEvent(eventId, transaction.Id, transaction.UserId, transaction.Amount, transaction.Category, transaction.CreatedAt);
            db.Add(new OutboxMessage { Id = eventId, EventType = nameof(TransactionCreatedEvent), Payload = JsonSerializer.Serialize(message) });
            await db.SaveChangesAsync(token);
        }, ct);
        logger.LogInformation("Transaction {TransactionId} and outbox event {EventId} created", transaction.Id, eventId);
        return Map(transaction);
    }
    public async Task<TransactionDto> GetAsync(long id, CancellationToken ct) => Map(await db.Transactions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new AppException("TransactionNotFound", $"Transaction with id {id} was not found.", 404));
    public async Task<List<TransactionDto>> GetForUserAsync(long userId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == userId, ct))
            throw new AppException("UserNotFound", $"User with id {userId} was not found.", 404);
        return await db.Transactions.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).Select(x => new TransactionDto(x.Id, x.UserId, x.Amount, x.Category, x.Merchant, x.Status, x.CreatedAt)).ToListAsync(ct);
    }
    private static TransactionDto Map(Transaction x) => new(x.Id, x.UserId, x.Amount, x.Category, x.Merchant, x.Status, x.CreatedAt);
}

