using Sheba.Ministry.Domain.Enums;
using Sheba.Shared.Kernel.Entities;

namespace Sheba.Ministry.Domain.Entities;

/// <summary>
/// Auth credentials for a ministry auth config.
/// All sensitive fields (API keys, tokens, passwords, client secrets) are encrypted
/// at rest with AES-256-GCM. The encryption/decryption happens in the Infrastructure
/// layer via ICredentialEncryptor.
///
/// This entity stores the ENCRYPTED values. The Application layer never sees plaintext
/// credentials — decryption happens only in the auth adapters at call time.
/// </summary>
public sealed class MinistryAuthCredential : BaseEntity
{
    public Guid AuthConfigId { get; private set; }

    // ── OIDC / OAuth2 fields ──────────────────────────────────────────────
    public string? OidcTokenEndpoint { get; private set; }
    public string? OidcClientId { get; private set; }       // encrypted
    public string? OidcClientSecret { get; private set; }   // encrypted
    public string? OidcScope { get; private set; }

    // ── API Key fields ────────────────────────────────────────────────────
    public string? ApiKeyHeaderName { get; private set; }
    public string? ApiKeyValue { get; private set; }        // encrypted
    public ApiKeyPlacement? ApiKeyPlacementType { get; private set; }

    // ── Bearer Token ──────────────────────────────────────────────────────
    public string? BearerToken { get; private set; }        // encrypted

    // ── Basic Auth ────────────────────────────────────────────────────────
    public string? BasicUsername { get; private set; }       // encrypted
    public string? BasicPassword { get; private set; }       // encrypted

    // ── Token cache (for OIDC/OAuth2) ─────────────────────────────────────
    public string? CachedAccessToken { get; private set; }   // encrypted
    public DateTime? TokenExpiresAt { get; private set; }

    // ── Audit ─────────────────────────────────────────────────────────────
    public DateTime? LastVerifiedAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    // EF Core
    private MinistryAuthCredential() { }

    /// <summary>Creates credentials for API Key auth. Values should be pre-encrypted.</summary>
    public static MinistryAuthCredential ForApiKey(
        Guid authConfigId,
        string headerName,
        string encryptedApiKeyValue,
        ApiKeyPlacement placement,
        Guid createdBy)
    {
        return new MinistryAuthCredential
        {
            AuthConfigId = authConfigId,
            ApiKeyHeaderName = headerName,
            ApiKeyValue = encryptedApiKeyValue,
            ApiKeyPlacementType = placement,
            CreatedBy = createdBy
        };
    }

    /// <summary>Creates credentials for Bearer Token auth. Value should be pre-encrypted.</summary>
    public static MinistryAuthCredential ForBearerToken(
        Guid authConfigId,
        string encryptedBearerToken,
        Guid createdBy)
    {
        return new MinistryAuthCredential
        {
            AuthConfigId = authConfigId,
            BearerToken = encryptedBearerToken,
            CreatedBy = createdBy
        };
    }

    /// <summary>Creates credentials for Basic Auth. Values should be pre-encrypted.</summary>
    public static MinistryAuthCredential ForBasicAuth(
        Guid authConfigId,
        string encryptedUsername,
        string encryptedPassword,
        Guid createdBy)
    {
        return new MinistryAuthCredential
        {
            AuthConfigId = authConfigId,
            BasicUsername = encryptedUsername,
            BasicPassword = encryptedPassword,
            CreatedBy = createdBy
        };
    }

    /// <summary>Creates credentials for OIDC/OAuth2 client_credentials. Values should be pre-encrypted.</summary>
    public static MinistryAuthCredential ForOidc(
        Guid authConfigId,
        string tokenEndpoint,
        string encryptedClientId,
        string encryptedClientSecret,
        string scope,
        Guid createdBy)
    {
        return new MinistryAuthCredential
        {
            AuthConfigId = authConfigId,
            OidcTokenEndpoint = tokenEndpoint,
            OidcClientId = encryptedClientId,
            OidcClientSecret = encryptedClientSecret,
            OidcScope = scope,
            CreatedBy = createdBy
        };
    }

    /// <summary>Creates empty credentials for None auth type.</summary>
    public static MinistryAuthCredential ForNone(Guid authConfigId, Guid createdBy)
    {
        return new MinistryAuthCredential
        {
            AuthConfigId = authConfigId,
            CreatedBy = createdBy
        };
    }

    public void UpdateCachedToken(string? encryptedToken, DateTime? expiresAt)
    {
        CachedAccessToken = encryptedToken;
        TokenExpiresAt = expiresAt;
        Touch();
    }

    public void RecordUsage()
    {
        LastUsedAt = DateTime.UtcNow;
        Touch();
    }

    public void RecordVerification()
    {
        LastVerifiedAt = DateTime.UtcNow;
        Touch();
    }
}
