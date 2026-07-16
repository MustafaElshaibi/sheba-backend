using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.ServiceRequest.Domain.Entities;

namespace Sheba.ServiceRequest.Infrastructure.Persistence.Configurations;

internal sealed class RequestStepExecutionConfiguration : IEntityTypeConfiguration<RequestStepExecution>
{
    public void Configure(EntityTypeBuilder<RequestStepExecution> b)
    {
        b.ToTable("request_step_executions");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(e => e.RequestId).HasColumnName("request_id").IsRequired();
        b.Property(e => e.StepId).HasColumnName("step_id").IsRequired();
        b.Property(e => e.StepOrder).HasColumnName("step_order").IsRequired();
        b.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(e => e.StartedAt).HasColumnName("started_at").IsRequired();
        b.Property(e => e.CompletedAt).HasColumnName("completed_at");
        b.Property(e => e.ActorId).HasColumnName("actor_id");
        b.Property(e => e.ActorType).HasColumnName("actor_type").HasMaxLength(30);
        b.Property(e => e.ResultJson).HasColumnName("result").HasColumnType("jsonb");
        b.Property(e => e.ErrorMessage).HasColumnName("error_message");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Ignore(e => e.DomainEvents);
    }
}
