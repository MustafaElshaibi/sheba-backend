using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.Ministry.Domain.Entities;
using Sheba.Ministry.Domain.Enums;

namespace Sheba.Ministry.Infrastructure.Persistence.Configurations;

internal sealed class MinistryEndpointConfiguration : IEntityTypeConfiguration<MinistryEndpoint>
{
    public void Configure(EntityTypeBuilder<MinistryEndpoint> builder)
    {
        builder.ToTable("ministry_endpoints");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.MinistryId).HasColumnName("ministry_id").IsRequired();
        builder.Property(e => e.AuthConfigId).HasColumnName("auth_config_id");
        builder.Property(e => e.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.Property(e => e.NameAr).HasColumnName("name_ar").HasMaxLength(300).IsRequired();
        builder.Property(e => e.NameEn).HasColumnName("name_en").HasMaxLength(300).IsRequired();
        builder.Property(e => e.DescriptionAr).HasColumnName("description_ar");
        builder.Property(e => e.DescriptionEn).HasColumnName("description_en");
        builder.Property(e => e.HttpMethod).HasColumnName("http_method").HasMaxLength(10).IsRequired();
        builder.Property(e => e.PathTemplate).HasColumnName("path_template").IsRequired();
        builder.Property(e => e.RequestSchemaJson).HasColumnName("request_schema").HasColumnType("jsonb");
        builder.Property(e => e.ResponseSchemaJson).HasColumnName("response_schema").HasColumnType("jsonb");
        builder.Property(e => e.Type).HasColumnName("endpoint_type").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(e => e.TimeoutSeconds).HasColumnName("timeout_seconds").HasDefaultValue(30);
        builder.Property(e => e.RateLimitPerMinute).HasColumnName("rate_limit_per_minute");
        builder.Property(e => e.RequiresCitizenConsent).HasColumnName("requires_citizen_consent").HasDefaultValue(false);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Ignore(e => e.DomainEvents);

        builder.HasIndex(e => new { e.MinistryId, e.Code })
            .IsUnique()
            .HasDatabaseName("ix_ministry_endpoints_ministry_code");
    }
}
