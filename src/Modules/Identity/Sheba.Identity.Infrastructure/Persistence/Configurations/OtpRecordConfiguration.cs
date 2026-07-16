using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;

namespace Sheba.Identity.Infrastructure.Persistence.Configurations;

internal sealed class OtpRecordConfiguration : IEntityTypeConfiguration<OtpRecord>
{
    public void Configure(EntityTypeBuilder<OtpRecord> builder)
    {
        builder.ToTable("otp_records");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(o => o.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.HasIndex(o => new { o.AccountId, o.Purpose })
            .HasDatabaseName("ix_otp_records_account_purpose");

        builder.Property(o => o.Purpose)
            .HasColumnName("purpose")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.Channel)
            .HasColumnName("channel")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.CodeHash)
            .HasColumnName("code_hash")
            .IsRequired();

        builder.Property(o => o.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(o => o.UsedAt)
            .HasColumnName("used_at");

        builder.Property(o => o.AttemptCount)
            .HasColumnName("attempt_count")
            .HasDefaultValue(0);

        builder.Property(o => o.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45); // IPv6 max

        builder.Property(o => o.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(500);

        builder.Property(o => o.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(o => o.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Ignore(o => o.DomainEvents);
    }
}
