using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RewardEngine.Domain;
using RewardEngine.Infrastructure;

namespace RewardEngine.Worker;

public sealed class TransactionConsumerService(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<TransactionConsumerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(configuration.GetConnectionString("RabbitMq")!),
            AutomaticRecoveryEnabled = false
        };
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await channel.QueueDeclareAsync("transaction-created.failed", true, false, false, cancellationToken: stoppingToken);
                await channel.QueueDeclareAsync("transaction-created", true, false, false, cancellationToken: stoppingToken);
                await channel.BasicQosAsync(0, 1, false, stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, args) =>
                    await HandleAsync(channel, args, stoppingToken);
                await channel.BasicConsumeAsync("transaction-created", false, consumer, stoppingToken);
                logger.LogInformation("RabbitMQ consumer started");
                while (connection.IsOpen && channel.IsOpen)
                    await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Consumer failed; reconnecting in five seconds");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs args, CancellationToken ct)
    {
        try
        {
            var message = JsonSerializer.Deserialize<TransactionCreatedEvent>(args.Body.Span)
                ?? throw new InvalidDataException("Empty event.");
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<RewardProcessor>().ProcessAsync(message, ct);
            await channel.BasicAckAsync(args.DeliveryTag, false, ct);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            try
            {
                // Preserve malformed messages for inspection using a confirmed publish.
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<RabbitMqEventPublisher>()
                    .PublishBodyAsync("transaction-created.failed", args.Body.ToArray(), ct);
                logger.LogError(ex, "Invalid message moved to transaction-created.failed");
                await channel.BasicAckAsync(args.DeliveryTag, false, ct);
            }
            catch (Exception publishError) when (!ct.IsCancellationRequested)
            {
                logger.LogError(publishError, "Failure-queue publication failed; preserving original delivery");
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                await channel.BasicNackAsync(args.DeliveryTag, false, true, ct);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Processing failed; retrying after delay");
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            await channel.BasicNackAsync(args.DeliveryTag, false, true, ct);
        }
    }
}
