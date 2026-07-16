using MediatR;
using Sheba.Ministry.Domain.Enums;

namespace Sheba.Ministry.Application.Commands.SetMinistryAuthConfig;

public sealed record SetMinistryAuthConfigCommand(
    Guid MinistryId,
    string Name,
    MinistryAuthType AuthType,
    string BaseUrl,
    bool IsDefault = false,
    string? HealthCheckPath = null,
    int TimeoutSeconds = 30,
    int RetryCount = 3,
    // Credential fields (plaintext — handler encrypts before storing)
    string? OidcTokenEndpoint = null,
    string? OidcClientId = null,
    string? OidcClientSecret = null,
    string? OidcScope = null,
    string? ApiKeyHeaderName = null,
    string? ApiKeyValue = null,
    ApiKeyPlacement? ApiKeyPlacementType = null,
    string? BearerToken = null,
    string? BasicUsername = null,
    string? BasicPassword = null,
    Guid? AdminId = null
) : IRequest<SetMinistryAuthConfigResponse>;

public sealed record SetMinistryAuthConfigResponse(Guid AuthConfigId, string Message);
