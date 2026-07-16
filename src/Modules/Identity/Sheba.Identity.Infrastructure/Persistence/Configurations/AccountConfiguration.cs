using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;

namespace Sheba.Identity.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedNever(); // We set it in BaseEntity

        builder.Property(a => a.NationalId)
            .HasColumnName("national_id")
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(a => a.NationalId)
            .IsUnique()
            .HasDatabaseName("ix_accounts_national_id");

        builder.Property(a => a.Username)
            .HasColumnName("username")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(a => a.Username)
            .IsUnique()
            .HasDatabaseName("ix_accounts_username");

        builder.Property(a => a.Email)
            .HasColumnName("email")
            .HasMaxLength(254)
            .IsRequired();

        builder.HasIndex(a => a.Email)
            .IsUnique()
            .HasDatabaseName("ix_accounts_email");

        builder.Property(a => a.PhoneNumber)
            .HasColumnName("phone_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.FullNameAr)
            .HasColumnName("full_name_ar")
            .HasMaxLength(300);

        builder.Property(a => a.FullNameEn)
            .HasColumnName("full_name_en")
            .HasMaxLength(300);

        builder.Property(a => a.EmailVerifiedAt)
            .HasColumnName("email_verified_at");

        builder.Property(a => a.PhoneVerifiedAt)
            .HasColumnName("phone_verified_at");

        builder.Property(a => a.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired();

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.IdentityLevel)
            .HasColumnName("identity_level")
            .HasDefaultValue(1);

        builder.Property(a => a.FailedLoginCount)
            .HasColumnName("failed_login_count")
            .HasDefaultValue(0);

        builder.Property(a => a.LockedUntil)
            .HasColumnName("locked_until");

        builder.Property(a => a.LastLoginAt)
            .HasColumnName("last_login_at");

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Domain events are not persisted
        builder.Ignore(a => a.DomainEvents);
    }
}
