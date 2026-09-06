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

public sealed class RabbitMqEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly ConnectionFactory _factory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqEventPublisher(IConfiguration configuration)
    {
        _factory = new ConnectionFactory
        {
            Uri = new Uri(configuration.GetConnectionString("RabbitMq")!),
            AutomaticRecoveryEnabled = false
        };
    }

    public Task PublishAsync(TransactionCreatedEvent message, CancellationToken ct) =>
        PublishBodyAsync("transaction-created", JsonSerializer.SerializeToUtf8Bytes(message), ct);

    public async Task PublishBodyAsync(string queue, byte[] body, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_connection is not { IsOpen: true } || _channel is not { IsOpen: true })
            {
                await CloseAsync();
                _connection = await _factory.CreateConnectionAsync(ct);
                _channel = await _connection.CreateChannelAsync(
                    new CreateChannelOptions(publisherConfirmationsEnabled: true,
                        publisherConfirmationTrackingEnabled: true), ct);
            }
            await _channel.QueueDeclareAsync(queue, true, false, false, cancellationToken: ct);
            var properties = new BasicProperties { Persistent = true, ContentType = "application/json" };
            // Completes after broker confirmation; mandatory rejects unroutable publications.
            await _channel.BasicPublishAsync(string.Empty, queue, true, properties, body, ct);
        }
        catch
        {
            await CloseAsync();
            throw;
        }
        finally { _gate.Release(); }
    }

    private async Task CloseAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();
        if (_connection is not null)
            await _connection.DisposeAsync();
        _channel = null;
        _connection = null;
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _gate.Dispose();
    }
}

