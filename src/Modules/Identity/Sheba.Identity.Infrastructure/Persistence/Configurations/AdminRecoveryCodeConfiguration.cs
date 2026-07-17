using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.Identity.Domain.Entities;

namespace Sheba.Identity.Infrastructure.Persistence.Configurations;

internal sealed class AdminRecoveryCodeConfiguration : IEntityTypeConfiguration<AdminRecoveryCode>
{
    public void Configure(EntityTypeBuilder<AdminRecoveryCode> builder)
    {
        builder.ToTable("admin_recovery_codes");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(c => c.AdminUserId)
            .HasColumnName("admin_user_id")
            .IsRequired();

        builder.HasIndex(c => c.AdminUserId)
            .HasDatabaseName("ix_admin_recovery_codes_admin_user_id");

        builder.Property(c => c.CodeHash)
            .HasColumnName("code_hash")
            .IsRequired();

        builder.Property(c => c.UsedAt)
            .HasColumnName("used_at");

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Ignore(c => c.DomainEvents);
    }
}
