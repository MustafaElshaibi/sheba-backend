using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.Ministry.Domain.Entities;
using Sheba.Ministry.Domain.Enums;

namespace Sheba.Ministry.Infrastructure.Persistence.Configurations;

internal sealed class MinistryAuthConfigConfiguration : IEntityTypeConfiguration<MinistryAuthConfig>
{
    public void Configure(EntityTypeBuilder<MinistryAuthConfig> builder)
    {
        builder.ToTable("ministry_auth_configs");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.MinistryId).HasColumnName("ministry_id").IsRequired();
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(c => c.AuthType).HasColumnName("auth_type").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(c => c.BaseUrl).HasColumnName("base_url").IsRequired();
        builder.Property(c => c.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(c => c.IsDefault).HasColumnName("is_default").HasDefaultValue(false);
        builder.Property(c => c.HealthCheckPath).HasColumnName("health_check_path");
        builder.Property(c => c.TimeoutSeconds).HasColumnName("timeout_seconds").HasDefaultValue(30);
        builder.Property(c => c.RetryCount).HasColumnName("retry_count").HasDefaultValue(3);
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Ignore(c => c.DomainEvents);

        builder.HasOne(c => c.Credential)
            .WithOne()
            .HasForeignKey<MinistryAuthCredential>(cr => cr.AuthConfigId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
