using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardEngine.Domain;

namespace RewardEngine.Application;

public sealed class CashbackRuleService(IRewardEngineDbContext db, ICashbackRuleCache cache)
{
    public async Task<CashbackRuleDto> CreateAsync(CreateCashbackRuleRequest request, CancellationToken ct)
    {
        Validate(request.Percentage, request.MonthlyLimit);
        if (await db.CashbackRules.AnyAsync(x => x.Category == request.Category, ct))
            throw new AppException("RuleAlreadyExists", $"A rule for {request.Category} already exists.", 409);
        var rule = new CashbackRule { Category = request.Category, Percentage = request.Percentage, MonthlyLimit = request.MonthlyLimit };
        db.Add(rule);
        await db.SaveChangesAsync(ct);
        await cache.InvalidateAsync(rule.Category, ct);
        return Map(rule);
    }
    public Task<List<CashbackRuleDto>> GetAllAsync(CancellationToken ct) => db.CashbackRules.AsNoTracking().OrderBy(x => x.Category).Select(x => new CashbackRuleDto(x.Id, x.Category, x.Percentage, x.MonthlyLimit, x.IsActive, x.CreatedAt, x.UpdatedAt)).ToListAsync(ct);
    public async Task<CashbackRuleDto> GetAsync(long id, CancellationToken ct) => Map(await Find(id, ct));
    public async Task<CashbackRuleDto> UpdateAsync(long id, UpdateCashbackRuleRequest request, CancellationToken ct)
    {
        Validate(request.Percentage, request.MonthlyLimit);
        var rule = await Find(id, ct);
        rule.Percentage = request.Percentage;
        rule.MonthlyLimit = request.MonthlyLimit;
        rule.IsActive = request.IsActive;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await cache.InvalidateAsync(rule.Category, ct);
        return Map(rule);
    }
    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        var rule = await Find(id, ct);
        rule.IsActive = false;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await cache.InvalidateAsync(rule.Category, ct);
    }
    private static void Validate(decimal percentage, decimal limit)
    {
        if (percentage < 0 || percentage > 100 || limit < 0 ||
            decimal.Round(percentage, 2) != percentage || decimal.Round(limit, 2) != limit)
            throw new AppException("ValidationFailed", "Percentage must be 0–100, limit must be nonnegative; use at most two decimal places.", 400);
    }
    private async Task<CashbackRule> Find(long id, CancellationToken ct) => await db.CashbackRules.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new AppException("CashbackRuleNotFound", $"Cashback rule with id {id} was not found.", 404);
    private static CashbackRuleDto Map(CashbackRule x) => new(x.Id, x.Category, x.Percentage, x.MonthlyLimit, x.IsActive, x.CreatedAt, x.UpdatedAt);
}
