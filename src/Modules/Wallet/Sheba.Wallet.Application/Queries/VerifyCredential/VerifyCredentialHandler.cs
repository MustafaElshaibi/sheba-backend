using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Wallet.Domain.Interfaces;

namespace Sheba.Wallet.Application.Queries.VerifyCredential;

public sealed class VerifyCredentialHandler(
    IWalletRepository repository,
    ICredentialSigner signer,
    ILogger<VerifyCredentialHandler> logger
) : IRequestHandler<VerifyCredentialQuery, VerifyCredentialResultDto>
{
    public async Task<VerifyCredentialResultDto> Handle(VerifyCredentialQuery query, CancellationToken ct)
    {
        // 1. Well-formed JWS shape (three dot-separated segments)?
        if (string.IsNullOrWhiteSpace(query.Jwt) || query.Jwt.Split('.').Length != 3)
            return Invalid("Malformed credential.");

        // 2. Signature genuinely from our issuer key — checked before any DB lookup so a
        // forged/foreign JWT can't be used to probe for credential existence via timing/response
        // differences.
        if (!signer.VerifyIssuerSignature(query.Jwt))
            return Invalid("Signature verification failed.");

        // 3. Confirm it's a credential we still have on record — a signature-valid JWT with no
        // matching row means the credential was purged after issuance (not expected in normal
        // operation, but this is a public endpoint fed attacker-controlled input, so fail closed
        // with a normal "invalid" result rather than a 500).
        var credential = await repository.GetCredentialByJwtAsync(query.Jwt, ct);
        if (credential is null)
        {
            logger.LogWarning("[VerifyCredential] Signature-valid JWT has no matching stored credential");
            return Invalid("Credential not recognized.");
        }

        if (credential.IsRevoked)
        {
            logger.LogInformation("[VerifyCredential] Credential {Id} presented but revoked", credential.Id);
            return Invalid("Credential has been revoked.", credential.Id);
        }

        if (credential.ExpiresAt is { } exp && exp <= DateTime.UtcNow)
            return Invalid("Credential has expired.", credential.Id);

        return new VerifyCredentialResultDto(
            IsValid: true,
            Reason: null,
            CredentialId: credential.Id,
            CredentialType: credential.CredentialType,
            IssuerDid: credential.IssuerDid,
            SubjectDid: credential.SubjectDid,
            Claims: DecodeClaims(credential.ClaimsJson),
            IssuedAt: credential.IssuedAt,
            ExpiresAt: credential.ExpiresAt);
    }

    private static VerifyCredentialResultDto Invalid(string reason, Guid? credentialId = null) =>
        new(false, reason, credentialId, null, null, null, null, null, null);

    private static Dictionary<string, object>? DecodeClaims(string claimsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(claimsJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
