using System.Net;
using System.Text;
using System.Text.Json;

namespace Bff.Api.Endpoints;

/// <summary>
/// BFF (Backend For Frontend): the single SCS origin the widget talks to.
/// Aggregates the two bounded contexts for reads and proxies writes to the
/// owning service. Keeps the widget simple and the contexts decoupled.
/// </summary>
public static class BffEndpoints
{
    public static IEndpointRouteBuilder MapBffApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").RequireAuthorization();

        group.MapGet("/dashboard", async (IHttpClientFactory http, HttpContext context, CancellationToken ct) =>
        {
            var todos = http.CreateClient("todos");
            var notifications = http.CreateClient("notifications");

            var todosTask = Proxy.ForwardAsync(todos, context, HttpMethod.Get, "/api/v1/todos", null, ct);
            var notificationsTask = Proxy.ForwardAsync(notifications, context, HttpMethod.Get, "/api/v1/notifications", null, ct);
            await Task.WhenAll(todosTask, notificationsTask);

            var todosResult = await todosTask;
            var notificationsResult = await notificationsTask;

            var warnings = new List<string>();
            var todosElement = DefaultIfUnavailable(todosResult, "todos-api", ref warnings);
            var notificationsElement = DefaultIfUnavailable(notificationsResult, "notifications-api", ref warnings);

            return Results.Json(new
            {
                todos = todosElement,
                notifications = notificationsElement,
                warnings,
                serverTime = DateTime.UtcNow,
            });
        });

        group.MapPost("/todos", (IHttpClientFactory http, HttpContext context, CreateTodoRequest request, CancellationToken ct) =>
            ForwardTo(http.CreateClient("todos"), context, HttpMethod.Post, "/api/v1/todos", request, ct));

        group.MapPost("/todos/{id:guid}/complete", (Guid id, IHttpClientFactory http, HttpContext context, CancellationToken ct) =>
            ForwardTo(http.CreateClient("todos"), context, HttpMethod.Post, $"/api/v1/todos/{id}/complete", null, ct));

        group.MapDelete("/todos/{id:guid}", (Guid id, IHttpClientFactory http, HttpContext context, CancellationToken ct) =>
            ForwardTo(http.CreateClient("todos"), context, HttpMethod.Delete, $"/api/v1/todos/{id}", null, ct));

        return app;
    }

    private static JsonElement DefaultIfUnavailable(ForwardResult result, string service, ref List<string> warnings)
    {
        if (result.Status == HttpStatusCode.OK && !string.IsNullOrWhiteSpace(result.Body))
            return JsonDocument.Parse(result.Body).RootElement.Clone();
        warnings.Add($"{service} unavailable (HTTP {(int)result.Status}); returning empty collection");
        return JsonSerializer.SerializeToElement(new object[0]);
    }

    private static IResult ForwardTo(HttpClient client, HttpContext context, HttpMethod method, string path, object? body, CancellationToken ct)
        => new ForwardingResult(async () =>
        {
            var result = await Proxy.ForwardAsync(client, context, method, path, body, ct);
            return new ForwardPayload(result.Body, result.StatusCode);
        });

    private sealed record CreateTodoRequest(string Title, string? Description);

    private sealed record ForwardPayload(string Body, int Status);

    /// <summary>Minimal IResult that streams a forwarded upstream response.</summary>
    private sealed class ForwardingResult(Func<Task<ForwardPayload>> action) : IResult
    {
        public async Task ExecuteAsync(HttpContext context)
        {
            var payload = await action();
            context.Response.StatusCode = payload.Status;
            if (!string.IsNullOrEmpty(payload.Body))
                await context.Response.WriteAsync(payload.Body, Encoding.UTF8);
        }
    }
}
