using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Ministry.Domain.Entities;
using Sheba.Ministry.Domain.Enums;
using Sheba.Ministry.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Ministry.Application.Commands.SetMinistryAuthConfig;

public sealed class SetMinistryAuthConfigHandler(
    IMinistryRepository repository,
    ICredentialEncryptor encryptor,
    ILogger<SetMinistryAuthConfigHandler> logger
) : IRequestHandler<SetMinistryAuthConfigCommand, SetMinistryAuthConfigResponse>
{
    public async Task<SetMinistryAuthConfigResponse> Handle(
        SetMinistryAuthConfigCommand request, CancellationToken ct)
    {
        var ministry = await repository.GetByIdAsync(request.MinistryId, ct)
            ?? throw new NotFoundException("Ministry", request.MinistryId);

        var authConfig = MinistryAuthConfig.Create(
            request.MinistryId,
            request.Name,
            request.AuthType,
            request.BaseUrl,
            request.IsDefault,
            request.HealthCheckPath,
            request.TimeoutSeconds,
            request.RetryCount);

        await repository.AddAuthConfigAsync(authConfig, ct);

        // Create credential based on auth type — encrypt all sensitive fields
        var adminId = request.AdminId ?? Guid.Empty;
        MinistryAuthCredential credential = request.AuthType switch
        {
            MinistryAuthType.ApiKey => MinistryAuthCredential.ForApiKey(
                authConfig.Id,
                request.ApiKeyHeaderName ?? "X-Api-Key",
                encryptor.Encrypt(request.ApiKeyValue ?? ""),
                request.ApiKeyPlacementType ?? ApiKeyPlacement.Header,
                adminId),

            MinistryAuthType.BearerToken => MinistryAuthCredential.ForBearerToken(
                authConfig.Id,
                encryptor.Encrypt(request.BearerToken ?? ""),
                adminId),

            MinistryAuthType.BasicAuth => MinistryAuthCredential.ForBasicAuth(
                authConfig.Id,
                encryptor.Encrypt(request.BasicUsername ?? ""),
                encryptor.Encrypt(request.BasicPassword ?? ""),
                adminId),

            MinistryAuthType.Oidc or MinistryAuthType.OAuth2 => MinistryAuthCredential.ForOidc(
                authConfig.Id,
                request.OidcTokenEndpoint ?? "",
                encryptor.Encrypt(request.OidcClientId ?? ""),
                encryptor.Encrypt(request.OidcClientSecret ?? ""),
                request.OidcScope ?? "api",
                adminId),

            _ => MinistryAuthCredential.ForNone(authConfig.Id, adminId)
        };

        await repository.AddCredentialAsync(credential, ct);
        await repository.SaveChangesAsync(ct);

        logger.LogInformation(
            "[SetMinistryAuthConfig] Created AuthConfig {Name} ({AuthType}) for Ministry {Id}",
            request.Name, request.AuthType, request.MinistryId);

        return new SetMinistryAuthConfigResponse(authConfig.Id, "Auth configuration created successfully.");
    }
}
