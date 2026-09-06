using System.Text.Json;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RewardEngine.Domain;
using RewardEngine.Infrastructure;

namespace RewardEngine.IntegrationTests;

public sealed class TransportTests
{
    [Fact]
    public async Task Confirmed_publication_can_be_read_from_real_broker()
    {
        await using var broker = new ContainerBuilder().WithImage("rabbitmq:3-management-alpine")
            .WithEnvironment("RABBITMQ_DEFAULT_USER", "test")
            .WithEnvironment("RABBITMQ_DEFAULT_PASS", "test")
            .WithPortBinding(5672, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5672)).Build();
        await broker.StartAsync();
        var uri = $"amqp://test:test@{broker.Hostname}:{broker.GetMappedPublicPort(5672)}/";
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:RabbitMq"] = uri }).Build();
        await using var publisher = new RabbitMqEventPublisher(config);
        var message = new TransactionCreatedEvent(Guid.NewGuid(), 1, 1, 100m,
            TransactionCategory.Restaurants, DateTimeOffset.UtcNow);
        await publisher.PublishAsync(message, CancellationToken.None);
        await using var connection = await new ConnectionFactory { Uri = new Uri(uri) }.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var delivery = await channel.BasicGetAsync("transaction-created", false);
        Assert.NotNull(delivery);
        Assert.Equal(message.EventId,
            JsonSerializer.Deserialize<TransactionCreatedEvent>(delivery.Body.Span)!.EventId);
        Assert.True(delivery.BasicProperties.Persistent);
        await channel.BasicAckAsync(delivery.DeliveryTag, false);
    }

    [Fact]
    public async Task Redis_supports_cache_invalidation_and_falls_back_when_stopped()
    {
        await using var redis = new ContainerBuilder().WithImage("redis:7-alpine")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379)).Build();
        await redis.StartAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = $"{redis.Hostname}:{redis.GetMappedPublicPort(6379)}"
            }).Build();
        await using var cache = new RedisCashbackRuleCache(config, NullLogger<RedisCashbackRuleCache>.Instance);
        var rule = new CashbackRule { Category = TransactionCategory.Taxi, Percentage = 10m, MonthlyLimit = 2000m };
        await cache.SetAsync(rule, CancellationToken.None);
        Assert.Equal(10m, (await cache.GetAsync(rule.Category, CancellationToken.None))!.Percentage);
        await cache.InvalidateAsync(rule.Category, CancellationToken.None);
        Assert.Null(await cache.GetAsync(rule.Category, CancellationToken.None));
        await redis.StopAsync();
        Assert.Null(await cache.GetAsync(rule.Category, CancellationToken.None));
    }
}

