using Microsoft.AspNetCore.Mvc;
using RewardEngine.Application;
namespace RewardEngine.Api.Controllers;
[ApiController, Route("api/cashback-rules")]
public sealed class CashbackRulesController(CashbackRuleService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CashbackRuleDto>> Create(CreateCashbackRuleRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new
        {
            id = result.Id
        }, result);
    }
    [HttpGet] public Task<List<CashbackRuleDto>> GetAll(CancellationToken ct) => service.GetAllAsync(ct);
    [HttpGet("{id:long}")] public Task<CashbackRuleDto> Get(long id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPut("{id:long}")] public Task<CashbackRuleDto> Update(long id, UpdateCashbackRuleRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
