using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.Identity.Domain.Entities;

namespace Sheba.Identity.Infrastructure.Persistence.Configurations;

internal sealed class RelyingPartyConfiguration : IEntityTypeConfiguration<RelyingParty>
{
    public void Configure(EntityTypeBuilder<RelyingParty> builder)
    {
        builder.ToTable("relying_parties");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.ClientId)
            .HasColumnName("client_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(r => r.ClientId)
            .IsUnique()
            .HasDatabaseName("ix_relying_parties_client_id");

        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.NameAr)
            .HasColumnName("name_ar")
            .HasMaxLength(200);

        builder.Property(r => r.Description)
            .HasColumnName("description");

        builder.Property(r => r.LogoUrl)
            .HasColumnName("logo_url");

        builder.Property(r => r.ClientType)
            .HasColumnName("client_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.PartyType)
            .HasColumnName("party_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.MinistryId)
            .HasColumnName("ministry_id");

        builder.Property(r => r.OrganizationId)
            .HasColumnName("organization_id");

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.RegisteredAt)
            .HasColumnName("registered_at")
            .IsRequired();

        builder.Property(r => r.RegisteredBy)
            .HasColumnName("registered_by")
            .IsRequired();

        builder.Property(r => r.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Ignore(r => r.DomainEvents);
        builder.Ignore(r => r.RedirectUris);
        builder.Ignore(r => r.Scopes);
    }
}

internal sealed class RpRedirectUriConfiguration : IEntityTypeConfiguration<RpRedirectUri>
{
    public void Configure(EntityTypeBuilder<RpRedirectUri> builder)
    {
        builder.ToTable("rp_redirect_uris");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.RelyingPartyId)
            .HasColumnName("relying_party_id")
            .IsRequired();

        builder.HasIndex(r => r.RelyingPartyId)
            .HasDatabaseName("ix_rp_redirect_uris_relying_party_id");

        builder.Property(r => r.Uri)
            .HasColumnName("uri")
            .IsRequired();

        builder.Property(r => r.UriType)
            .HasColumnName("uri_type")
            .HasMaxLength(20)
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

internal sealed class RpScopeConfiguration : IEntityTypeConfiguration<RpScope>
{
    public void Configure(EntityTypeBuilder<RpScope> builder)
    {
        builder.ToTable("rp_scopes");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.RelyingPartyId)
            .HasColumnName("relying_party_id")
            .IsRequired();

        builder.HasIndex(r => new { r.RelyingPartyId, r.ScopeName })
            .IsUnique()
            .HasDatabaseName("ix_rp_scopes_party_scope");

        builder.Property(r => r.ScopeName)
            .HasColumnName("scope_name")
            .HasMaxLength(100)
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