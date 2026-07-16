using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sheba.Ministry.Domain.Entities;
using Sheba.Ministry.Domain.Enums;

namespace Sheba.Ministry.Infrastructure.Persistence.Configurations;

internal sealed class MinistryAuthCredentialConfiguration : IEntityTypeConfiguration<MinistryAuthCredential>
{
    public void Configure(EntityTypeBuilder<MinistryAuthCredential> builder)
    {
        builder.ToTable("ministry_auth_credentials");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.AuthConfigId).HasColumnName("auth_config_id").IsRequired();
        builder.Property(c => c.OidcTokenEndpoint).HasColumnName("oidc_token_endpoint");
        builder.Property(c => c.OidcClientId).HasColumnName("oidc_client_id");
        builder.Property(c => c.OidcClientSecret).HasColumnName("oidc_client_secret");
        builder.Property(c => c.OidcScope).HasColumnName("oidc_scope");
        builder.Property(c => c.ApiKeyHeaderName).HasColumnName("api_key_header_name");
        builder.Property(c => c.ApiKeyValue).HasColumnName("api_key_value");
        builder.Property(c => c.ApiKeyPlacementType).HasColumnName("api_key_placement").HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.BearerToken).HasColumnName("bearer_token");
        builder.Property(c => c.BasicUsername).HasColumnName("basic_username");
        builder.Property(c => c.BasicPassword).HasColumnName("basic_password");
        builder.Property(c => c.CachedAccessToken).HasColumnName("cached_access_token");
        builder.Property(c => c.TokenExpiresAt).HasColumnName("token_expires_at");
        builder.Property(c => c.LastVerifiedAt).HasColumnName("last_verified_at");
        builder.Property(c => c.LastUsedAt).HasColumnName("last_used_at");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Ignore(c => c.DomainEvents);
    }
}
