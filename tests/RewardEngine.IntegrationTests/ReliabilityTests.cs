using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RewardEngine.Application;
using RewardEngine.Domain;
using RewardEngine.Infrastructure;
using Testcontainers.PostgreSql;

namespace RewardEngine.IntegrationTests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").Build();
    public string ConnectionString => _postgres.GetConnectionString();
    public RewardEngineDbContext Open() => new(new DbContextOptionsBuilder<RewardEngineDbContext>()
        .UseNpgsql(ConnectionString).Options);
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = Open();
        await db.Database.MigrateAsync();
    }
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}

public sealed class CacheMiss : ICashbackRuleCache
{
    public Task<CashbackRule?> GetAsync(TransactionCategory category, CancellationToken ct) =>
        Task.FromResult<CashbackRule?>(null);
    public Task SetAsync(CashbackRule rule, CancellationToken ct) => Task.CompletedTask;
    public Task InvalidateAsync(TransactionCategory category, CancellationToken ct) => Task.CompletedTask;
}

public sealed class ApiFactory(string connection) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", connection);
        builder.ConfigureServices(services => services.AddSingleton<ICashbackRuleCache, CacheMiss>());
    }
}

public sealed class ReliabilityTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private async Task<TransactionCreatedEvent> Purchase(long? userId = null,
        decimal amount = 5000m, TransactionCategory category = TransactionCategory.Restaurants,
        DateTimeOffset? date = null)
    {
        await using var db = fixture.Open();
        if (userId is null)
        {
            var user = new User { Name = "Integration" };
            db.UserSet.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        }
        var purchase = new Transaction
        {
            UserId = userId.Value,
            Amount = amount,
            Category = category,
            Merchant = "Shop",
            CreatedAt = date ?? DateTimeOffset.UtcNow
        };
        db.TransactionSet.Add(purchase);
        await db.SaveChangesAsync();
        return new(Guid.NewGuid(), purchase.Id, purchase.UserId, amount, category, purchase.CreatedAt);
    }

    private async Task Process(TransactionCreatedEvent message)
    {
        await using var db = fixture.Open();
        var processor = new RewardProcessor(db, new CacheMiss(), new CashbackCalculator(),
            NullLogger<RewardProcessor>.Instance);
        await processor.ProcessAsync(message, CancellationToken.None);
    }

    [Fact]
    public async Task Duplicate_events_and_new_event_ids_cannot_reward_twice()
    {
        var message = await Purchase();
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Process(message)));
        await Process(message with
        {
            EventId = Guid.NewGuid()
        });
        await using var db = fixture.Open();
        Assert.Equal(1, await db.CashbackRewardSet.CountAsync(x => x.TransactionId == message.TransactionId));
        Assert.Equal(250m, await db.CashbackRewardSet.Where(x => x.TransactionId == message.TransactionId).SumAsync(x => x.Amount));
    }

    [Fact]
    public async Task Concurrent_purchases_cannot_exceed_category_limit()
    {
        var first = await Purchase(amount: 50000m);
        var second = await Purchase(first.UserId, 50000m);
        await Task.WhenAll(Process(first), Process(second));
        await using var db = fixture.Open();
        Assert.Equal(3000m, await db.CashbackRewardSet.Where(x => x.UserId == first.UserId).SumAsync(x => x.Amount));
    }

    [Fact]
    public async Task Limits_are_separate_by_category_and_utc_purchase_month()
    {
        var first = await Purchase(amount: 60000m, date: new DateTimeOffset(2025, 12, 31, 23, 59, 0, TimeSpan.Zero));
        await Process(first);
        await Process(await Purchase(first.UserId, 5000m, TransactionCategory.Taxi, first.CreatedAt));
        await Process(await Purchase(first.UserId, 5000m, date: first.CreatedAt.AddMinutes(2)));
        await using var db = fixture.Open();
        Assert.Equal(3750m, await db.CashbackRewardSet.Where(x => x.UserId == first.UserId).SumAsync(x => x.Amount));
    }

    [Fact]
    public async Task Forged_event_is_rejected_without_writes()
    {
        var message = await Purchase();
        await Assert.ThrowsAsync<InvalidDataException>(() => Process(message with { Amount = 9000m }));
        await using var db = fixture.Open();
        Assert.False(await db.ProcessedEventSet.AnyAsync(x => x.EventId == message.EventId));
        Assert.False(await db.CashbackRewardSet.AnyAsync(x => x.TransactionId == message.TransactionId));
    }

    [Fact]
    public async Task Api_commits_outbox_and_returns_real_reward_history()
    {
        await using var api = new ApiFactory(fixture.ConnectionString);
        var client = api.CreateClient();
        var response = await client.PostAsJsonAsync("/api/users", new
        {
            name = "Tigran"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = (await response.Content.ReadFromJsonAsync<UserDto>())!;
        response = await client.PostAsJsonAsync("/api/transactions",
            new
            {
                userId = user.Id,
                amount = 5000m,
                category = "Restaurants",
                merchant = "Shop"
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var purchase = (await response.Content.ReadFromJsonAsync<TransactionDto>(Json))!;
        await using var db = fixture.Open();
        var payloads = await db.OutboxMessageSet.Select(x => x.Payload).ToListAsync();
        var message = payloads.Select(x => JsonSerializer.Deserialize<TransactionCreatedEvent>(x)!)
            .Single(x => x.TransactionId == purchase.Id);
        await Process(message);
        var history = await client.GetFromJsonAsync<List<CashbackHistoryItemDto>>(
            $"/api/users/{user.Id}/cashback/history", Json);
        Assert.Equal(250m, Assert.Single(history!).CashbackAmount);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
    }

    [Theory]
    [InlineData(0, "Restaurants")]
    [InlineData(1.001, "Restaurants")]
    [InlineData(100, "Invalid")]
    [InlineData(100, "999")]
    public async Task Api_rejects_invalid_transactions(decimal amount, string category)
    {
        await using var api = new ApiFactory(fixture.ConnectionString);
        var client = api.CreateClient();
        var response = await client.PostAsJsonAsync("/api/transactions",
            new
            {
                userId = 1,
                amount,
                category,
                merchant = "Shop"
            });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Database_unique_constraint_rejects_second_reward()
    {
        var message = await Purchase();
        await Process(message);
        await using var db = fixture.Open();
        db.CashbackRewardSet.Add(new CashbackReward
        {
            UserId = message.UserId,
            TransactionId = message.TransactionId,
            Amount = 1m,
            Percentage = 1m
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Failed_database_transaction_rolls_back_purchase_and_outbox()
    {
        await using var db = fixture.Open();
        var user = new User { Name = "Rollback" };
        db.UserSet.Add(user);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.ExecuteInTransactionAsync(async ct =>
        {
            var service = new TransactionService(db, NullLogger<TransactionService>.Instance);
            await service.CreateAsync(new(user.Id, 100m, TransactionCategory.Other, "Shop"), ct);
            throw new InvalidOperationException("Simulated failure before outer commit");
        }, CancellationToken.None));
        await using var verify = fixture.Open();
        Assert.False(await verify.TransactionSet.AnyAsync(x => x.UserId == user.Id));
        var payloads = await verify.OutboxMessageSet.Select(x => x.Payload).ToListAsync();
        Assert.DoesNotContain(payloads, x => JsonSerializer.Deserialize<TransactionCreatedEvent>(x)!.UserId == user.Id);
    }
}
