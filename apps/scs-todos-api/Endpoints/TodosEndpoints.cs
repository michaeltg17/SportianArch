using System.Security.Claims;
using Todos.Api.Application;
using Todos.Api.Domain;
using Todos.Api.Infrastructure;

namespace Todos.Api.Endpoints;

public static class TodosEndpoints
{
    public static IEndpointRouteBuilder MapTodosApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/todos").RequireAuthorization();

        group.MapPost("/", async (CreateTodoRequest request, ITodoStore store, HttpContext http, CancellationToken ct) =>
        {
            var userId = GetUserId(http);
            var todo = new Todo(Guid.NewGuid(), userId, request.Title, request.Description);
            await store.AddAsync(todo, ct);
            return Results.Created($"/api/v1/todos/{todo.Id}", todo.ToDto());
        });

        group.MapGet("/", async (ITodoStore store, HttpContext http, CancellationToken ct) =>
            Results.Ok(await store.ListAsync(GetUserId(http), ct)));

        group.MapGet("/{id:guid}", async (Guid id, ITodoStore store, HttpContext http, CancellationToken ct) =>
        {
            var todo = await store.GetByIdAsync(id, GetUserId(http), ct);
            return todo is null ? Results.NotFound() : Results.Ok(todo.ToDto());
        });

        group.MapPost("/{id:guid}/complete", async (Guid id, ITodoStore store, IEventPublisher publisher, HttpContext http, CancellationToken ct) =>
        {
            var userId = GetUserId(http);
            var todo = await store.GetByIdAsync(id, userId, ct);
            if (todo is null)
                return Results.NotFound();

            try
            {
                todo.Complete();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }

            await store.UpdateAsync(todo, ct);

            var completedAt = todo.CompletedAt!.Value;
            await publisher.PublishAsync(
                "todo.completed",
                Guid.NewGuid().ToString("N"),
                new TodoCompleted(Guid.NewGuid().ToString("N"), todo.Id, todo.Title, todo.UserId, completedAt),
                ct);

            return Results.Ok(todo.ToDto());
        });

        group.MapDelete("/{id:guid}", async (Guid id, ITodoStore store, HttpContext http, CancellationToken ct) =>
        {
            await store.DeleteAsync(id, GetUserId(http), ct);
            return Results.NoContent();
        });

        return app;
    }

    private static string GetUserId(HttpContext http) =>
        http.User.FindFirstValue("sub") ?? "anonymous";
}
