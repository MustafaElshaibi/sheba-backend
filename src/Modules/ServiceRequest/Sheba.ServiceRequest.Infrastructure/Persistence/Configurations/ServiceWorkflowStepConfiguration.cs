using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.ServiceRequest.Domain.Entities;

namespace Sheba.ServiceRequest.Infrastructure.Persistence.Configurations;

internal sealed class ServiceWorkflowStepConfiguration : IEntityTypeConfiguration<ServiceWorkflowStep>
{
    public void Configure(EntityTypeBuilder<ServiceWorkflowStep> b)
    {
        b.ToTable("service_workflow_steps");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(e => e.ServiceId).HasColumnName("service_id").IsRequired();
        b.Property(e => e.StepOrder).HasColumnName("step_order").IsRequired();
        b.Property(e => e.NameAr).HasColumnName("name_ar").HasMaxLength(200).IsRequired();
        b.Property(e => e.NameEn).HasColumnName("name_en").HasMaxLength(200).IsRequired();
        b.Property(e => e.StepType).HasColumnName("step_type").HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(e => e.Actor).HasColumnName("actor").HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(e => e.MinistryEndpointId).HasColumnName("ministry_endpoint_id");
        b.Property(e => e.TimeoutHours).HasColumnName("timeout_hours");
        b.Property(e => e.IsAutomated).HasColumnName("is_automated").HasDefaultValue(false);
        b.Property(e => e.OnSuccessStep).HasColumnName("on_success_step");
        b.Property(e => e.OnFailureStep).HasColumnName("on_failure_step");
        b.Property(e => e.ConfigJson).HasColumnName("config").HasColumnType("jsonb");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Ignore(e => e.DomainEvents);
    }
}
