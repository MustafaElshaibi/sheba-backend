using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.Admin.Domain.Entities;

namespace Sheba.Admin.Infrastructure.Persistence.Configurations;

internal sealed class DailyServiceRequestSnapshotConfiguration
    : IEntityTypeConfiguration<DailyServiceRequestSnapshot>
{
    public void Configure(EntityTypeBuilder<DailyServiceRequestSnapshot> builder)
    {
        builder.ToTable("analytics_service_requests_daily");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(s => s.Date)
            .HasColumnName("date")
            .IsRequired();

        builder.Property(s => s.ServiceId)
            .HasColumnName("service_id")
            .IsRequired();

        builder.Property(s => s.MinistryId)
            .HasColumnName("ministry_id")
            .IsRequired();

        // Composite unique index on (date, service_id) per the architecture spec
        builder.HasIndex(s => new { s.Date, s.ServiceId })
            .IsUnique()
            .HasDatabaseName("ix_analytics_sr_daily_date_service");

        builder.Property(s => s.Submitted)
            .HasColumnName("submitted")
            .HasDefaultValue(0);

        builder.Property(s => s.Completed)
            .HasColumnName("completed")
            .HasDefaultValue(0);

        builder.Property(s => s.Rejected)
            .HasColumnName("rejected")
            .HasDefaultValue(0);

        builder.Property(s => s.Cancelled)
            .HasColumnName("cancelled")
            .HasDefaultValue(0);

        builder.Property(s => s.SlaBreach)
            .HasColumnName("sla_breached")
            .HasDefaultValue(0);

        builder.Property(s => s.AvgCompletionHours)
            .HasColumnName("avg_completion_hours")
            .HasPrecision(8, 2);

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Ignore(s => s.DomainEvents);
    }
}
