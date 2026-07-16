using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.Admin.Domain.Entities;
using Sheba.Admin.Domain.Enums;

namespace Sheba.Admin.Infrastructure.Persistence.Configurations;

internal sealed class ReportJobConfiguration : IEntityTypeConfiguration<ReportJob>
{
    public void Configure(EntityTypeBuilder<ReportJob> builder)
    {
        builder.ToTable("report_jobs");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(r => r.ReportType)
            .HasColumnName("report_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.Format)
            .HasColumnName("format")
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.FiltersJson)
            .HasColumnName("filters")
            .HasColumnType("jsonb");

        builder.Property(r => r.RequestedBy)
            .HasColumnName("requested_by")
            .IsRequired();

        builder.Property(r => r.HangfireJobId)
            .HasColumnName("hangfire_job_id")
            .HasMaxLength(100);

        builder.Property(r => r.FileBytes)
            .HasColumnName("file_bytes");

        builder.Property(r => r.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(255);

        builder.Property(r => r.FileSizeBytes)
            .HasColumnName("file_size_bytes");

        builder.Property(r => r.RowCount)
            .HasColumnName("row_count");

        builder.Property(r => r.ErrorMessage)
            .HasColumnName("error_message");

        builder.Property(r => r.StartedAt)
            .HasColumnName("started_at");

        builder.Property(r => r.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Ignore(r => r.DomainEvents);
    }
}
