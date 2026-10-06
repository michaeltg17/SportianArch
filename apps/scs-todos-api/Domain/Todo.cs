namespace Todos.Api.Domain;

/// <summary>Aggregate root of the Todo bounded context.</summary>
public sealed class Todo
{
    public Guid Id { get; }
    public string UserId { get; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public bool IsCompleted { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Todo(Guid id, string userId, string title, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        Title = NormalizeTitle(title);
        Id = id;
        UserId = userId;
        Description = NormalizeDescription(description);
        CreatedAt = DateTime.UtcNow;
    }

    public void Rename(string title, string? description = null)
    {
        Title = NormalizeTitle(title);
        Description = NormalizeDescription(description);
    }

    public void Complete()
    {
        if (IsCompleted)
            throw new InvalidOperationException("Todo is already completed.");
        IsCompleted = true;
        CompletedAt = DateTime.UtcNow;
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        var trimmed = title.Trim();
        if (trimmed.Length > 200)
            throw new ArgumentException("Title must be at most 200 characters.", nameof(title));
        return trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        if (trimmed is { Length: > 2000 })
            throw new ArgumentException("Description must be at most 2000 characters.", nameof(description));
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
