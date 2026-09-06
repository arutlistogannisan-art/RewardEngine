using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardEngine.Domain;

namespace RewardEngine.Application;

public sealed class CashbackQueryService(IRewardEngineDbContext db)
{
    public async Task<CashbackSummaryDto> GetSummaryAsync(long userId, CancellationToken ct)
    {
        await EnsureUser(userId, ct);
        var total = await db.CashbackRewards.Where(x => x.UserId == userId).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        return new(userId, total);
    }
    public async Task<List<CashbackHistoryItemDto>> GetHistoryAsync(long userId, CancellationToken ct)
    {
        await EnsureUser(userId, ct);
        return await db.CashbackRewards.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).Select(x => new CashbackHistoryItemDto(x.Id, x.TransactionId, x.Transaction.Amount, x.Transaction.Category, x.Transaction.Merchant, x.Amount, x.Percentage, x.CreatedAt)).ToListAsync(ct);
    }
    private async Task EnsureUser(long id, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == id, ct))
            throw new AppException("UserNotFound", $"User with id {id} was not found.", 404);
    }
}

