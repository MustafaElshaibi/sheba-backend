using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.Admin.Domain.Entities;

namespace Sheba.Admin.Infrastructure.Persistence.Configurations;

internal sealed class DailyRegistrationSnapshotConfiguration
    : IEntityTypeConfiguration<DailyRegistrationSnapshot>
{
    public void Configure(EntityTypeBuilder<DailyRegistrationSnapshot> builder)
    {
        builder.ToTable("analytics_identity_daily");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(s => s.Date)
            .HasColumnName("date")
            .IsRequired();

        builder.HasIndex(s => s.Date)
            .IsUnique()
            .HasDatabaseName("ix_analytics_identity_daily_date");

        builder.Property(s => s.TotalRegistrations)
            .HasColumnName("total_registrations")
            .HasDefaultValue(0);

        builder.Property(s => s.Approved)
            .HasColumnName("approved")
            .HasDefaultValue(0);

        builder.Property(s => s.Rejected)
            .HasColumnName("rejected")
            .HasDefaultValue(0);

        builder.Property(s => s.PendingEod)
            .HasColumnName("pending_eod")
            .HasDefaultValue(0);

        builder.Property(s => s.AvgApprovalHours)
            .HasColumnName("avg_approval_hours")
            .HasPrecision(6, 2);

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Ignore(s => s.DomainEvents);
    }
}
