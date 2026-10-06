using Microsoft.EntityFrameworkCore;
using Notifications.Api.Domain;

namespace Notifications.Api.Infrastructure;

/// <summary>
/// Database-per-service: this context talks only to the scs_notifications database.
/// Demo uses EnsureCreated(); a production SCS would ship versioned migrations.
/// </summary>
public class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Id).ValueGeneratedNever();
            entity.Property(n => n.UserId).HasMaxLength(64).IsRequired();
            entity.Property(n => n.Type).HasMaxLength(100).IsRequired();
            entity.Property(n => n.Message).HasMaxLength(500).IsRequired();
            entity.Property(n => n.SourceId);
            entity.Property(n => n.CreatedAt).IsRequired();
            entity.HasIndex(n => new { n.UserId, n.CreatedAt });
        });
    }
}
