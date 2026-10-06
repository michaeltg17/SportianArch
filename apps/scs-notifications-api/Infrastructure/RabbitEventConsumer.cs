using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Notifications.Api.Domain;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Notifications.Api.Infrastructure;

/// <summary>
/// Consumes "todo.completed" events from the scs.todos topic exchange.
/// Idempotent: events are deduplicated by (SourceId, Type), so at-least-once
/// delivery from the broker never creates duplicate notifications.
/// </summary>
public sealed class RabbitEventConsumer(
    IConnectionFactory connectionFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitEventConsumer> logger) : BackgroundService
{
    public const string Exchange = "scs.todos";
    public const string Queue = "notifications.todo.completed";
    public const string RoutingKey = "todo.completed";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeUntilShutdownAsync(stoppingToken);
                logger.LogWarning("RabbitMQ connection closed; reconnecting in 5s");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning("RabbitMQ unavailable ({Message}); retrying in 5s", ex.Message);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task ConsumeUntilShutdownAsync(CancellationToken ct)
    {
        var connection = await connectionFactory.CreateConnectionAsync(ct);
        using var channel = await connection.CreateChannelAsync(ScsRabbit.CreateChannelOptions(), ct);

        await channel.BasicQosAsync(0, 1, global: false, ct);

        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, autoDelete: false, arguments: null, passive: false, noWait: false, ct);
        await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false, arguments: null, passive: false, noWait: false, ct);
        await channel.QueueBindAsync(Queue, Exchange, RoutingKey, arguments: null, noWait: false, ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                await HandleAsync(channel, ea);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed processing message {DeliveryTag}; nacking (no requeue)", ea.DeliveryTag);
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        await channel.BasicConsumeAsync(Queue, autoAck: false, consumer);
        logger.LogInformation("Consuming queue {Queue} bound to {Exchange} ({RoutingKey})", Queue, Exchange, RoutingKey);

        // Block until the broker connection drops, then let the retry loop reconnect.
        var shutdown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.ConnectionShutdownAsync += (_, args) =>
        {
            if (args.Initiator == ShutdownInitiator.Peer)
                shutdown.TrySetResult();
            return Task.CompletedTask;
        };
        await shutdown.Task;
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs ea)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<INotificationStore>();

        var body = ea.Body.ToArray();
        var evt = JsonSerializer.Deserialize<TodoCompletedEvent>(body, Json);
        if (evt is null || evt.TodoId == Guid.Empty || string.IsNullOrWhiteSpace(evt.UserId))
        {
            logger.LogWarning("Dropping malformed message {MessageId}", ea.BasicProperties.MessageId);
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            return;
        }

        // Idempotency: at-least-once delivery must not create duplicates.
        if (await store.ExistsBySourceAsync(evt.TodoId, NotificationTypes.TodoCompleted))
        {
            logger.LogInformation("Duplicate event for todo {TodoId} ignored", evt.TodoId);
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            return;
        }

        await store.AddAsync(new Notification(
            Guid.NewGuid(),
            evt.UserId,
            NotificationTypes.TodoCompleted,
            $"Todo \"{evt.Title}\" was completed.",
            evt.TodoId));

        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
        logger.LogInformation("Stored notification for todo {TodoId} (user {UserId})", evt.TodoId, evt.UserId);
    }
}
