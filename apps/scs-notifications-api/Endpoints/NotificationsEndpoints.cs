using System.Security.Claims;
using Notifications.Api.Domain;

namespace Notifications.Api.Endpoints;

public static class NotificationsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationsApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications").RequireAuthorization();

        group.MapGet("/", async (INotificationStore store, HttpContext http, CancellationToken ct) =>
        {
            var userId = http.User.FindFirstValue("sub") ?? "anonymous";
            return Results.Ok(await store.ListByUserAsync(userId, take: 50, ct));
        });

        return app;
    }
}
