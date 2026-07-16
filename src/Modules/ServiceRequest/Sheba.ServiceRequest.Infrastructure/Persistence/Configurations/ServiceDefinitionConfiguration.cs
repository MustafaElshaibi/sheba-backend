using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.ServiceRequest.Domain.Entities;

namespace Sheba.ServiceRequest.Infrastructure.Persistence.Configurations;

internal sealed class ServiceDefinitionConfiguration : IEntityTypeConfiguration<ServiceDefinition>
{
    public void Configure(EntityTypeBuilder<ServiceDefinition> b)
    {
        b.ToTable("services");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(e => e.CategoryId).HasColumnName("category_id").IsRequired();
        b.Property(e => e.MinistryId).HasColumnName("ministry_id").IsRequired();
        b.Property(e => e.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        b.HasIndex(e => e.Code).IsUnique().HasDatabaseName("ix_services_code");
        b.Property(e => e.NameAr).HasColumnName("name_ar").HasMaxLength(300).IsRequired();
        b.Property(e => e.NameEn).HasColumnName("name_en").HasMaxLength(300).IsRequired();
        b.Property(e => e.DescriptionAr).HasColumnName("description_ar");
        b.Property(e => e.DescriptionEn).HasColumnName("description_en");
        b.Property(e => e.EligibilityRulesJson).HasColumnName("eligibility_rules").HasColumnType("jsonb");
        b.Property(e => e.RequiredLoa).HasColumnName("required_loa").HasDefaultValue(1);
        b.Property(e => e.RequiresAppointment).HasColumnName("requires_appointment").HasDefaultValue(false);
        b.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(e => e.IsOnline).HasColumnName("is_online").HasDefaultValue(true);
        b.Property(e => e.AverageDays).HasColumnName("average_days");
        b.Property(e => e.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        b.Property(e => e.TagsCsv).HasColumnName("tags");
        b.Property(e => e.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Ignore(e => e.DomainEvents);

        b.HasOne(e => e.FormSchema).WithOne().HasForeignKey<ServiceFormSchema>(f => f.ServiceId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(e => e.Fees).WithOne().HasForeignKey(f => f.ServiceId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(e => e.RequiredDocuments).WithOne().HasForeignKey(d => d.ServiceId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(e => e.WorkflowSteps).WithOne().HasForeignKey(s => s.ServiceId).OnDelete(DeleteBehavior.Cascade);
    }
}
