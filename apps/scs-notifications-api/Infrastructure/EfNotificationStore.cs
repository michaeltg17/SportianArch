using Microsoft.EntityFrameworkCore;
using Notifications.Api.Domain;

namespace Notifications.Api.Infrastructure;

public sealed class EfNotificationStore(IDbContextFactory<NotificationsDbContext> factory) : INotificationStore
{
    public async Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Notification>> ListByUserAsync(string userId, int take = 50, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsBySourceAsync(Guid sourceId, string type, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Notifications
            .AnyAsync(n => n.SourceId == sourceId && n.Type == type, ct);
    }
}
