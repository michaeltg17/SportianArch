using System.Text.Json;
using RabbitMQ.Client;
using Todos.Api.Domain;

namespace Todos.Api.Infrastructure;

/// <summary>
/// Publishes SCS integration events to RabbitMQ.
/// Asynchronous, eventual-consistency communication: the Todo context never
/// waits on or calls the Notification context.
/// </summary>
public sealed class RabbitEventPublisher(IConnectionFactory connectionFactory) : IEventPublisher
{
    public const string Exchange = "scs.todos";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task PublishAsync<TPayload>(string routingKey, string messageId, TPayload payload, CancellationToken ct = default)
        where TPayload : class
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(ScsRabbit.CreateChannelOptions(), ct);
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, autoDelete: false, arguments: null, passive: false, noWait: false, ct);

        var body = JsonSerializer.SerializeToUtf8Bytes(payload, Json);
        var properties = new BasicProperties
        {
            Persistent = true,
            DeliveryMode = DeliveryModes.Persistent,
            ContentType = "application/json",
            MessageId = messageId,
        };

        await channel.BasicPublishAsync(Exchange, routingKey, mandatory: true, properties, body, ct);
    }
}
