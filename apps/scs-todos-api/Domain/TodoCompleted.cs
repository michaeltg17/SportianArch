namespace Todos.Api.Domain;

/// <summary>
/// Integration event published to the "scs.todos" topic exchange
/// (routing key "todo.completed"). JSON shape must stay in sync with
/// TodoCompletedEvent in packages/shared.
/// </summary>
public sealed record TodoCompleted(
    string MessageId,
    Guid TodoId,
    string Title,
    string UserId,
    DateTime CompletedAt);
