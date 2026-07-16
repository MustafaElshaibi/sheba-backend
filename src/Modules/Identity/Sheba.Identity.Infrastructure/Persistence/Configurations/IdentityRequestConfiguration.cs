using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;

namespace Sheba.Identity.Infrastructure.Persistence.Configurations;

internal sealed class IdentityRequestConfiguration : IEntityTypeConfiguration<IdentityRequest>
{
    public void Configure(EntityTypeBuilder<IdentityRequest> builder)
    {
        builder.ToTable("identity_requests");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(r => r.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        // Logical FK (UUID reference — no DB-level FK constraint per architecture rules)
        builder.HasIndex(r => r.AccountId)
            .HasDatabaseName("ix_identity_requests_account_id");

        builder.Property(r => r.RequestType)
            .HasColumnName("request_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.SubmittedAt)
            .HasColumnName("submitted_at")
            .IsRequired();

        builder.Property(r => r.ReviewedAt)
            .HasColumnName("reviewed_at");

        builder.Property(r => r.ReviewedByAdminId)
            .HasColumnName("reviewed_by_admin_id");

        builder.Property(r => r.RejectionReason)
            .HasColumnName("rejection_reason")
            .HasMaxLength(2000);

        builder.Property(r => r.AdminNotes)
            .HasColumnName("admin_notes")
            .HasMaxLength(2000);

        builder.Property(r => r.CitizenSnapshotJson)
            .HasColumnName("citizen_snapshot")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Ignore(r => r.DomainEvents);
    }
}
