namespace Todos.Api.Domain;

public interface IEventPublisher
{
    /// <summary>Publishes a JSON event to the SCS topic exchange (fire-and-forget, no synchronous dependencies).</summary>
    Task PublishAsync<TPayload>(string routingKey, string messageId, TPayload payload, CancellationToken ct = default)
        where TPayload : class;
}
