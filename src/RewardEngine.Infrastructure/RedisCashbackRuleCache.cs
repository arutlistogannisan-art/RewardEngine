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

public sealed class RedisCashbackRuleCache : ICashbackRuleCache, IAsyncDisposable
{
    private readonly ILogger<RedisCashbackRuleCache> _logger;
    private readonly Lazy<Task<ConnectionMultiplexer>> _connection;

    public RedisCashbackRuleCache(IConfiguration configuration, ILogger<RedisCashbackRuleCache> logger)
    {
        _logger = logger;
        var options = ConfigurationOptions.Parse(configuration.GetConnectionString("Redis") ?? "localhost:6379");
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 1000;
        options.AsyncTimeout = 1000;
        options.BacklogPolicy = BacklogPolicy.FailFast;
        _connection = new(() => ConnectionMultiplexer.ConnectAsync(options));
    }

    public async Task<CashbackRule?> GetAsync(TransactionCategory category, CancellationToken ct)
    {
        try
        {
            var db = (await _connection.Value.WaitAsync(ct)).GetDatabase();
            var value = await db.StringGetAsync(Key(category)).WaitAsync(ct);
            return value.HasValue ? JsonSerializer.Deserialize<CashbackRule>(value.ToString()) : null;
        }
        catch (Exception ex) when (ex is RedisException or JsonException)
        {
            _logger.LogWarning(ex, "Rule cache read failed; using PostgreSQL");
            return null;
        }
    }

    public async Task SetAsync(CashbackRule rule, CancellationToken ct)
    {
        try
        {
            var db = (await _connection.Value.WaitAsync(ct)).GetDatabase();
            // Short TTL bounds staleness if invalidation fails or races with a cache fill.
            await db.StringSetAsync(Key(rule.Category), JsonSerializer.Serialize(rule),
                TimeSpan.FromSeconds(30)).WaitAsync(ct);
        }
        catch (RedisException ex) { _logger.LogWarning(ex, "Rule cache write failed"); }
    }

    public async Task InvalidateAsync(TransactionCategory category, CancellationToken ct)
    {
        try
        {
            var db = (await _connection.Value.WaitAsync(ct)).GetDatabase();
            await db.KeyDeleteAsync(Key(category)).WaitAsync(ct);
        }
        catch (RedisException ex) { _logger.LogWarning(ex, "Rule invalidation failed; TTL bounds staleness"); }
    }

    private static string Key(TransactionCategory category) => $"cashback-rule:{category}";

    public async ValueTask DisposeAsync()
    {
        if (_connection.IsValueCreated)
            await (await _connection.Value).DisposeAsync();
    }
}

