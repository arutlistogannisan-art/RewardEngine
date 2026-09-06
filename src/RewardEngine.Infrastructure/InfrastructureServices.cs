using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RewardEngine.Application;
using RewardEngine.Domain;
using StackExchange.Redis;

namespace RewardEngine.Infrastructure;

public static class InfrastructureServices
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<RewardEngineDbContext>(o => o.UseNpgsql(configuration.GetConnectionString("Postgres")));
        services.AddScoped<IRewardEngineDbContext>(sp => sp.GetRequiredService<RewardEngineDbContext>());
        services.AddSingleton<ICashbackRuleCache, RedisCashbackRuleCache>();
        services.AddSingleton<CashbackCalculator>();
        services.AddScoped<RewardProcessor>();
        services.AddSingleton<RabbitMqEventPublisher>();
        services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<RabbitMqEventPublisher>());
        return services;
    }
}
