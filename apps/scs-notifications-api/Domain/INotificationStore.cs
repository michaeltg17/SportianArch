namespace Notifications.Api.Domain;

public interface INotificationStore
{
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> ListByUserAsync(string userId, int take = 50, CancellationToken ct = default);
    Task<bool> ExistsBySourceAsync(Guid sourceId, string type, CancellationToken ct = default);
}
