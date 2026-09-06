using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RewardEngine.Infrastructure;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RewardEngineDbContext>
{
    public RewardEngineDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<RewardEngineDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                ?? "Host=localhost;Database=rewardengine;Username=rewardengine;Password=rewardengine_dev")
            .Options);
}
