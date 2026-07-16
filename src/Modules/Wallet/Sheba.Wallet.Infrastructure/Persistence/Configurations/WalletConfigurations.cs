using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.Wallet.Domain.Entities;

namespace Sheba.Wallet.Infrastructure.Persistence.Configurations;

internal sealed class VerifiableCredentialConfiguration : IEntityTypeConfiguration<VerifiableCredential>
{
    public void Configure(EntityTypeBuilder<VerifiableCredential> b)
    {
        b.ToTable("verifiable_credentials");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(e => e.SubjectId).HasColumnName("subject_id").IsRequired();
        b.HasIndex(e => e.SubjectId).HasDatabaseName("ix_vc_subject");
        b.Property(e => e.CredentialType).HasColumnName("credential_type").HasMaxLength(100).IsRequired();
        b.Property(e => e.IssuerDid).HasColumnName("issuer_did").HasMaxLength(200).IsRequired();
        b.Property(e => e.SubjectDid).HasColumnName("subject_did").HasMaxLength(200).IsRequired();
        b.Property(e => e.Jwt).HasColumnName("jwt").IsRequired();
        b.Property(e => e.ClaimsJson).HasColumnName("claims").HasColumnType("jsonb").IsRequired();
        b.Property(e => e.IssuedAt).HasColumnName("issued_at").IsRequired();
        b.Property(e => e.ExpiresAt).HasColumnName("expires_at");
        b.Property(e => e.IsRevoked).HasColumnName("is_revoked").HasDefaultValue(false);
        b.Property(e => e.RevokedAt).HasColumnName("revoked_at");
        b.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Ignore(e => e.DomainEvents);
    }
}

internal sealed class DidDocumentConfiguration : IEntityTypeConfiguration<DidDocument>
{
    public void Configure(EntityTypeBuilder<DidDocument> b)
    {
        b.ToTable("did_documents");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(e => e.Did).HasColumnName("did").HasMaxLength(200).IsRequired();
        b.HasIndex(e => e.Did).IsUnique().HasDatabaseName("ix_did_documents_did");
        b.Property(e => e.SubjectId).HasColumnName("subject_id");
        b.Property(e => e.PublicKeyPem).HasColumnName("public_key_pem").IsRequired();
        b.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Ignore(e => e.DomainEvents);
    }
}

internal sealed class CredentialSchemaConfiguration : IEntityTypeConfiguration<CredentialSchema>
{
    public void Configure(EntityTypeBuilder<CredentialSchema> b)
    {
        b.ToTable("credential_schemas");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(e => e.SchemaUri).HasColumnName("schema_uri").HasMaxLength(300).IsRequired();
        b.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        b.Property(e => e.Version).HasColumnName("version").HasMaxLength(20);
        b.Property(e => e.IssuerDid).HasColumnName("issuer_did").HasMaxLength(200);
        b.Property(e => e.SchemaDefinitionJson).HasColumnName("schema_definition").HasColumnType("jsonb");
        b.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Ignore(e => e.DomainEvents);
    }
}
