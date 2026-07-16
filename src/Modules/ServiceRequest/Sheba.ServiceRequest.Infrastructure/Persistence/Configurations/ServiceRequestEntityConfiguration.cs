using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.ServiceRequest.Domain.Entities;

namespace Sheba.ServiceRequest.Infrastructure.Persistence.Configurations;

internal sealed class ServiceRequestEntityConfiguration : IEntityTypeConfiguration<ServiceRequestEntity>
{
    public void Configure(EntityTypeBuilder<ServiceRequestEntity> b)
    {
        b.ToTable("service_requests");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(e => e.ReferenceNumber).HasColumnName("reference_number").HasMaxLength(20).IsRequired();
        b.HasIndex(e => e.ReferenceNumber).IsUnique().HasDatabaseName("ix_service_requests_ref");
        b.Property(e => e.ServiceId).HasColumnName("service_id").IsRequired();
        b.Property(e => e.CitizenId).HasColumnName("citizen_id").IsRequired();
        b.HasIndex(e => e.CitizenId).HasDatabaseName("ix_service_requests_citizen");
        b.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(e => e.CurrentStep).HasColumnName("current_step").HasDefaultValue(1);
        b.Property(e => e.FormDataJson).HasColumnName("form_data").HasColumnType("jsonb");
        b.Property(e => e.Priority).HasColumnName("priority").HasMaxLength(20).HasDefaultValue("NORMAL");
        b.Property(e => e.SubmittedAt).HasColumnName("submitted_at").IsRequired();
        b.Property(e => e.CompletedAt).HasColumnName("completed_at");
        b.Property(e => e.DueDate).HasColumnName("due_date");
        b.Property(e => e.RejectionReason).HasColumnName("rejection_reason");
        b.Property(e => e.Notes).HasColumnName("notes");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Ignore(e => e.DomainEvents);

        b.HasMany(e => e.StepExecutions).WithOne().HasForeignKey(s => s.RequestId).OnDelete(DeleteBehavior.Cascade);
    }
}
