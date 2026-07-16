using Sheba.Ministry.Domain.Entities;

namespace Sheba.Ministry.Domain.Interfaces;

/// <summary>
/// Result of authenticating and making a test call to a ministry endpoint.
/// </summary>
public sealed record MinistryConnectionTestResult(
    bool Success,
    int? StatusCode,
    long LatencyMs,
    string? Error);

/// <summary>
/// Port (interface) for ministry authentication adapters.
/// Each auth type (OIDC, ApiKey, BasicAuth, BearerToken) has a concrete adapter
/// in Sheba.Ministry.Infrastructure.Adapters.
///
/// The adapter is responsible for:
///   1. Decrypting stored credentials via ICredentialEncryptor
///   2. Building the correct HTTP auth headers
///   3. Managing token caches for OIDC/OAuth2
///   4. Testing connectivity to the ministry's API
/// </summary>
public interface IMinistryAuthAdapter
{
    /// <summary>The auth type this adapter handles.</summary>
    string AdapterType { get; }

    /// <summary>
    /// Applies authentication to the given HttpRequestMessage.
    /// The adapter reads encrypted credentials from the DB, decrypts them,
    /// and sets the appropriate Authorization header / query params.
    /// </summary>
    Task AuthenticateRequestAsync(
        HttpRequestMessage request,
        MinistryAuthConfig authConfig,
        MinistryAuthCredential credential,
        CancellationToken ct = default);

    /// <summary>
    /// Tests connectivity to the ministry API by calling the health check endpoint.
    /// Returns a result with success/failure, latency, and any error message.
    /// </summary>
    Task<MinistryConnectionTestResult> TestConnectionAsync(
        MinistryAuthConfig authConfig,
        MinistryAuthCredential credential,
        CancellationToken ct = default);
}
