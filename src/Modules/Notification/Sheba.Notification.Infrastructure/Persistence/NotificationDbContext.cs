using Microsoft.EntityFrameworkCore;
using Sheba.Notification.Domain.Entities;
using Sheba.Shared.Kernel.Outbox;

namespace Sheba.Notification.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Notification module.
/// Mapped exclusively to the "notification" PostgreSQL schema.
///
/// Architecture rule: No other module may reference or inject this DbContext.
/// </summary>
public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options)
    : DbContext(options)
{
    public DbSet<NotificationRecord> NotificationRecords => Set<NotificationRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notification");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
