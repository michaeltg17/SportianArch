using Microsoft.EntityFrameworkCore;
using Todos.Api.Domain;

namespace Todos.Api.Infrastructure;

/// <summary>
/// Database-per-service: this context talks only to the scs_todos database.
/// Demo uses EnsureCreated(); a production SCS would ship versioned migrations.
/// </summary>
public class TodosDbContext(DbContextOptions<TodosDbContext> options) : DbContext(options)
{
    public DbSet<Todo> Todos => Set<Todo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Todo>(entity =>
        {
            entity.ToTable("todos");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.UserId).HasMaxLength(64).IsRequired();
            entity.Property(t => t.Title).HasMaxLength(200).IsRequired();
            entity.Property(t => t.Description).HasMaxLength(2000);
            entity.HasIndex(t => t.UserId);
        });
    }
}
