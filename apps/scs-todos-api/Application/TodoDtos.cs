namespace Todos.Api.Application;

/// <summary>Versioned API contract (v1). camelCase JSON.</summary>
public sealed record TodoDto(
    Guid Id,
    string Title,
    string? Description,
    bool IsCompleted,
    DateTime? CompletedAt,
    DateTime CreatedAt);

public sealed record CreateTodoRequest(string Title, string? Description);

public static class TodoMappers
{
    public static TodoDto ToDto(this Domain.Todo todo) =>
        new(todo.Id, todo.Title, todo.Description, todo.IsCompleted, todo.CompletedAt, todo.CreatedAt);
}
