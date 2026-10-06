namespace Todos.Api.Domain;

public interface ITodoStore
{
    Task<Todo?> GetByIdAsync(Guid id, string userId, CancellationToken ct = default);
    Task<IReadOnlyList<Todo>> ListAsync(string userId, CancellationToken ct = default);
    Task AddAsync(Todo todo, CancellationToken ct = default);
    Task UpdateAsync(Todo todo, CancellationToken ct = default);
    Task DeleteAsync(Guid id, string userId, CancellationToken ct = default);
}
