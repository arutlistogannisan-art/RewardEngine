using Microsoft.AspNetCore.Mvc;
using RewardEngine.Application;
namespace RewardEngine.Api.Controllers;
[ApiController, Route("api/transactions")]
public sealed class TransactionsController(TransactionService service) : ControllerBase
{
    [HttpPost, ProducesResponseType<TransactionDto>(201), ProducesResponseType(400), ProducesResponseType(404)]
    public async Task<ActionResult<TransactionDto>> Create(CreateTransactionRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new
        {
            id = result.Id
        }, result);
    }
    [HttpGet("{id:long}")] public Task<TransactionDto> Get(long id, CancellationToken ct) => service.GetAsync(id, ct);
}
