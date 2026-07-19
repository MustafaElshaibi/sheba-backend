using System.Text.Json;
using MediatR;
using Sheba.Shared.Kernel.Exceptions;
using Sheba.Wallet.Application.Queries.GetMyCredentials;
using Sheba.Wallet.Domain.Interfaces;

namespace Sheba.Wallet.Application.Queries.GetCredentialById;

public sealed class GetCredentialByIdHandler(IWalletRepository repository)
    : IRequestHandler<GetCredentialByIdQuery, CredentialDto>
{
    public async Task<CredentialDto> Handle(GetCredentialByIdQuery query, CancellationToken ct)
    {
        var credential = await repository.GetCredentialByIdAsync(query.CredentialId, ct)
            ?? throw new NotFoundException("VerifiableCredential", query.CredentialId);

        // NotFoundException (not Forbidden) for a non-owner — anti-enumeration posture consistent
        // with the rest of the codebase: don't confirm another citizen's credential exists.
        if (!query.IsAdmin && credential.SubjectId != query.ActorId)
            throw new NotFoundException("VerifiableCredential", query.CredentialId);

        return new CredentialDto(
            credential.Id, credential.CredentialType, credential.IssuerDid, credential.SubjectDid, credential.Jwt,
            DecodeClaims(credential.ClaimsJson),
            credential.IssuedAt, credential.ExpiresAt, credential.IsRevoked);
    }

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
