using Microsoft.EntityFrameworkCore;
using Sheba.Audit.Domain.Entities;

namespace Sheba.Audit.Infrastructure.Persistence;

/// <summary>
/// Audit schema DbContext — append-only by design.
/// The application DB user should be granted only INSERT on audit.audit_events.
/// No Update/Delete operations are exposed.
/// </summary>
public sealed class AuditDbContext : DbContext
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema("audit");

        mb.Entity<AuditEvent>(e =>
        {
            e.ToTable("audit_events");
            e.HasKey(x => x.Id);

            e.Property(x => x.ActorId).IsRequired();
            e.Property(x => x.Action).HasMaxLength(256).IsRequired();
            e.Property(x => x.EntityType).HasMaxLength(128);
            e.Property(x => x.Timestamp).IsRequired();
            e.Property(x => x.IpAddress).HasMaxLength(45); // IPv6 max
            e.Property(x => x.RequestSnapshot).HasColumnType("jsonb");
            e.Property(x => x.ResponseSnapshot).HasColumnType("jsonb");
            e.Property(x => x.ErrorMessage).HasMaxLength(2000);

            // Indexes for admin query filtering
            e.HasIndex(x => x.ActorId);
            e.HasIndex(x => x.EntityType);
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.Action);
        });

        base.OnModelCreating(mb);
    }
}
