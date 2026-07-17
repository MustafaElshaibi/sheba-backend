using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.Identity.Domain.Entities;

namespace Sheba.Identity.Infrastructure.Persistence.Configurations;

internal sealed class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> builder)
    {
        builder.ToTable("admin_users");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(a => a.EmployeeId)
            .HasColumnName("employee_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(a => a.EmployeeId)
            .IsUnique()
            .HasDatabaseName("ix_admin_users_employee_id");

        builder.Property(a => a.Email)
            .HasColumnName("email")
            .HasMaxLength(254)
            .IsRequired();

        builder.HasIndex(a => a.Email)
            .IsUnique()
            .HasDatabaseName("ix_admin_users_email");

        builder.Property(a => a.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Department)
            .HasColumnName("department")
            .HasMaxLength(100);

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired();

        builder.Property(a => a.MfaSecret)
            .HasColumnName("mfa_secret");

        builder.Property(a => a.MfaEnabled)
            .HasColumnName("mfa_enabled")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.MfaFailedAttempts)
            .HasColumnName("mfa_failed_attempts")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(a => a.MfaLockedUntil)
            .HasColumnName("mfa_locked_until");

        builder.Property(a => a.MinistryId)
            .HasColumnName("ministry_id");

        builder.HasIndex(a => a.MinistryId)
            .HasDatabaseName("ix_admin_users_ministry_id");

        builder.Property(a => a.LastLoginAt)
            .HasColumnName("last_login_at");

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Ignore(a => a.DomainEvents);
    }
}