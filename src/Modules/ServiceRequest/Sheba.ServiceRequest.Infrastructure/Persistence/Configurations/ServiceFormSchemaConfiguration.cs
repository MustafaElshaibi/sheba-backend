using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.ServiceRequest.Domain.Entities;

namespace Sheba.ServiceRequest.Infrastructure.Persistence.Configurations;

internal sealed class ServiceFormSchemaConfiguration : IEntityTypeConfiguration<ServiceFormSchema>
{
    public void Configure(EntityTypeBuilder<ServiceFormSchema> b)
    {
        b.ToTable("service_forms");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(e => e.ServiceId).HasColumnName("service_id").IsRequired();
        b.HasIndex(e => e.ServiceId).IsUnique().HasDatabaseName("ix_service_forms_service_id");
        b.Property(e => e.SchemaVersion).HasColumnName("schema_version").HasMaxLength(20).HasDefaultValue("1.0");
        b.Property(e => e.FormSchemaJson).HasColumnName("form_schema").HasColumnType("jsonb").IsRequired();
        b.Property(e => e.UiSchemaJson).HasColumnName("ui_schema").HasColumnType("jsonb");
        b.Property(e => e.ValidationRulesJson).HasColumnName("validation_rules").HasColumnType("jsonb");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Ignore(e => e.DomainEvents);
    }
}
