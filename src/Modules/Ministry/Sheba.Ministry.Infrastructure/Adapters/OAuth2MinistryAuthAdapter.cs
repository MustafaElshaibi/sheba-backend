using Sheba.Ministry.Domain.Entities;
using Sheba.Ministry.Domain.Interfaces;

namespace Sheba.Ministry.Infrastructure.Adapters;

/// <summary>
/// OAuth2 client_credentials adapter — delegates to the OIDC adapter since the flow is identical.
/// </summary>
public sealed class OAuth2MinistryAuthAdapter(OidcMinistryAuthAdapter inner) : IMinistryAuthAdapter
{
    public string AdapterType => "OAuth2";

    public Task AuthenticateRequestAsync(
        HttpRequestMessage request,
        MinistryAuthConfig authConfig,
        MinistryAuthCredential credential,
        CancellationToken ct = default)
        => inner.AuthenticateRequestAsync(request, authConfig, credential, ct);

    public Task<MinistryConnectionTestResult> TestConnectionAsync(
        MinistryAuthConfig authConfig,
        MinistryAuthCredential credential,
        CancellationToken ct = default)
        => inner.TestConnectionAsync(authConfig, credential, ct);
}
