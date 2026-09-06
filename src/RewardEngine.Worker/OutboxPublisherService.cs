using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RewardEngine.Domain;
using RewardEngine.Infrastructure;
namespace RewardEngine.Worker;
public sealed class OutboxPublisherService(IServiceScopeFactory scopes, ILogger<OutboxPublisherService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<RewardEngineDbContext>();
                var publisher = scope.ServiceProvider.GetRequiredService<RewardEngine.Application.IEventPublisher>();
                var now = DateTimeOffset.UtcNow;
                var messages = await db.OutboxMessageSet.Where(x => x.ProcessedAt == null && (x.NextAttemptAt == null || x.NextAttemptAt <= now)).OrderBy(x => x.CreatedAt).Take(20).ToListAsync(stoppingToken);
                foreach (var message in messages)
                {
                    try
                    {
                        var e = JsonSerializer.Deserialize<TransactionCreatedEvent>(message.Payload) ?? throw new InvalidOperationException("Invalid outbox payload");
                        await publisher.PublishAsync(e, stoppingToken);
                        message.ProcessedAt = DateTimeOffset.UtcNow;
                        message.LastError = null;
                        logger.LogInformation("Published RabbitMQ event {EventId}", message.Id);
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        message.RetryCount++;
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, Math.Min(message.RetryCount, 5)));
                        message.NextAttemptAt = DateTimeOffset.UtcNow.Add(delay);
                        message.LastError = ex.GetType().Name;
                        logger.LogError(ex, "RabbitMQ publish failed for event {EventId}; retry {RetryCount}",
                            message.Id, message.RetryCount);
                    }
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogError(ex, "Outbox worker iteration failed"); }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
