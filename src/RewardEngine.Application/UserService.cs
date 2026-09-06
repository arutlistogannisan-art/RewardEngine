using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardEngine.Domain;

namespace RewardEngine.Application;

public sealed class UserService(IRewardEngineDbContext db)
{
    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
            throw new AppException("ValidationFailed", "Name must not be empty.", 400);
        var user = new User { Name = name };
        db.Add(user);
        await db.SaveChangesAsync(ct);
        return Map(user);
    }
    public Task<List<UserDto>> GetAllAsync(CancellationToken ct) => db.Users.AsNoTracking().OrderBy(x => x.Id).Select(x => new UserDto(x.Id, x.Name, x.CreatedAt)).ToListAsync(ct);
    public async Task<UserDto> GetAsync(long id, CancellationToken ct) => Map(await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new AppException("UserNotFound", $"User with id {id} was not found.", 404));
    private static UserDto Map(User x) => new(x.Id, x.Name, x.CreatedAt);
}

