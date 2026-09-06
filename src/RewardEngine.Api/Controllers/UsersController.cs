using Microsoft.AspNetCore.Mvc;
using RewardEngine.Application;
namespace RewardEngine.Api.Controllers;
[ApiController, Route("api/users")]
public sealed class UsersController(UserService users, TransactionService transactions, CashbackQueryService cashback) : ControllerBase
{
    [HttpPost, ProducesResponseType<UserDto>(201), ProducesResponseType(400)]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var result = await users.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new
        {
            id = result.Id
        }, result);
    }
    [HttpGet] public Task<List<UserDto>> GetAll(CancellationToken ct) => users.GetAllAsync(ct);
    [HttpGet("{id:long}")] public Task<UserDto> Get(long id, CancellationToken ct) => users.GetAsync(id, ct);
    [HttpGet("{userId:long}/transactions")] public Task<List<TransactionDto>> GetTransactions(long userId, CancellationToken ct) => transactions.GetForUserAsync(userId, ct);
    [HttpGet("{userId:long}/cashback")] public Task<CashbackSummaryDto> GetCashback(long userId, CancellationToken ct) => cashback.GetSummaryAsync(userId, ct);
    [HttpGet("{userId:long}/cashback/history")] public Task<List<CashbackHistoryItemDto>> GetCashbackHistory(long userId, CancellationToken ct) => cashback.GetHistoryAsync(userId, ct);
}
