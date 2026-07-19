using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.Admin.Domain.Entities;

namespace Sheba.Admin.Infrastructure.Persistence.Configurations;

internal sealed class DailyRevenueSnapshotConfiguration : IEntityTypeConfiguration<DailyRevenueSnapshot>
{
    public void Configure(EntityTypeBuilder<DailyRevenueSnapshot> builder)
    {
        builder.ToTable("analytics_revenue_daily");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(s => s.Date)
            .HasColumnName("date")
            .IsRequired();

        builder.Property(s => s.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.HasIndex(s => new { s.Date, s.Currency })
            .IsUnique()
            .HasDatabaseName("ix_analytics_revenue_daily_date_currency");

        builder.Property(s => s.TotalAmount)
            .HasColumnName("total_amount")
            .HasPrecision(14, 2)
            .HasDefaultValue(0m);

        builder.Property(s => s.PaymentsCompleted)
            .HasColumnName("payments_completed")
            .HasDefaultValue(0);

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Ignore(s => s.DomainEvents);
    }
}
