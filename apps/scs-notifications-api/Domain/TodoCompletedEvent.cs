namespace Notifications.Api.Domain;

/// <summary>
/// Inbound integration event, deserialized from RabbitMQ.
/// JSON shape must stay in sync with TodoCompleted in scs-todos-api and
/// TodoCompletedEvent in packages/shared.
/// </summary>
public sealed record TodoCompletedEvent(
    string MessageId,
    Guid TodoId,
    string Title,
    string UserId,
    string CompletedAt);

public static class NotificationTypes
{
    public const string TodoCompleted = "todo.completed";
}
