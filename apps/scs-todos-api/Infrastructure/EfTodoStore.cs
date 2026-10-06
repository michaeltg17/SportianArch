using Microsoft.EntityFrameworkCore;
using Todos.Api.Domain;

namespace Todos.Api.Infrastructure;

public sealed class EfTodoStore(IDbContextFactory<TodosDbContext> factory) : ITodoStore
{
    public async Task<Todo?> GetByIdAsync(Guid id, string userId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Todos.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);
    }

    public async Task<IReadOnlyList<Todo>> ListAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Todos
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Todo todo, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Todos.Add(todo);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Todo todo, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Todos.Update(todo);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, string userId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var todo = await db.Todos.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);
        if (todo is null)
            return;
        db.Todos.Remove(todo);
        await db.SaveChangesAsync(ct);
    }
}
