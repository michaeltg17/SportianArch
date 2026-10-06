namespace Notifications.Api.Domain;

/// <summary>Aggregate of the Notification bounded context.</summary>
public sealed class Notification
{
    public Guid Id { get; }
    public string UserId { get; }
    public string Type { get; }
    public string Message { get; }
    public Guid? SourceId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Notification(Guid id, string userId, string type, string message, Guid? sourceId = null)
    {
        Id = id;
        UserId = userId;
        Type = type;
        Message = message;
        SourceId = sourceId;
        CreatedAt = DateTime.UtcNow;
    }
}
