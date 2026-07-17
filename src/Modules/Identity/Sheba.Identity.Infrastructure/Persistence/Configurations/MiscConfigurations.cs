using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.Identity.Domain.Entities;

namespace Sheba.Identity.Infrastructure.Persistence.Configurations;

internal sealed class ScopeDefinitionConfiguration : IEntityTypeConfiguration<ScopeDefinition>
{
    public void Configure(EntityTypeBuilder<ScopeDefinition> builder)
    {
        builder.ToTable("scope_definitions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(s => s.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(s => s.Name)
            .IsUnique()
            .HasDatabaseName("ix_scope_definitions_name");

        builder.Property(s => s.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.DisplayNameAr)
            .HasColumnName("display_name_ar")
            .HasMaxLength(200);

        builder.Property(s => s.Description)
            .HasColumnName("description");

        builder.Property(s => s.Claims)
            .HasColumnName("claims")
            .HasColumnType("text[]");

        builder.Property(s => s.IsSystem)
            .HasColumnName("is_system");

        builder.Property(s => s.RequiresLoa)
            .HasColumnName("requires_loa")
            .HasDefaultValue(1);

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Ignore(s => s.DomainEvents);
    }
}

internal sealed class RefreshTokenFamilyConfiguration : IEntityTypeConfiguration<RefreshTokenFamily>
{
    public void Configure(EntityTypeBuilder<RefreshTokenFamily> builder)
    {
        builder.ToTable("refresh_token_families");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.SubjectId)
            .HasColumnName("subject_id")
            .IsRequired();

        builder.HasIndex(r => r.SubjectId)
            .HasDatabaseName("ix_refresh_token_families_subject_id");

        builder.Property(r => r.ClientId)
            .HasColumnName("client_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.FamilyId)
            .HasColumnName("family_id")
            .IsRequired();

        builder.HasIndex(r => r.FamilyId)
            .IsUnique()
            .HasDatabaseName("ix_refresh_token_families_family_id");

        builder.Property(r => r.Generation)
            .HasColumnName("generation")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(r => r.IssuedAt)
            .HasColumnName("issued_at");

        builder.Property(r => r.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(r => r.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(r => r.RevocationReason)
            .HasColumnName("revocation_reason")
            .HasMaxLength(50);

        builder.Property(r => r.DeviceFingerprint)
            .HasColumnName("device_fingerprint");

        builder.Property(r => r.IpAddress)
            .HasColumnName("ip_address");

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Ignore(r => r.DomainEvents);
    }
}

internal sealed class IdentityRequestDocumentConfiguration : IEntityTypeConfiguration<IdentityRequestDocument>
{
    public void Configure(EntityTypeBuilder<IdentityRequestDocument> builder)
    {
        builder.ToTable("identity_request_documents");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(d => d.RequestId)
            .HasColumnName("request_id")
            .IsRequired();

        builder.HasIndex(d => d.RequestId)
            .HasDatabaseName("ix_identity_request_documents_request_id");

        builder.Property(d => d.DocumentType)
            .HasColumnName("document_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.DocumentServiceId)
            .HasColumnName("document_service_id")
            .IsRequired();

        builder.Property(d => d.UploadedAt)
            .HasColumnName("uploaded_at");

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Ignore(d => d.DomainEvents);
    }
}