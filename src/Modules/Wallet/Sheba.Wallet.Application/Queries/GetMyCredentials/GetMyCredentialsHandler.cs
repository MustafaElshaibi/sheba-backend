using System.Text.Json;
using MediatR;
using Sheba.Wallet.Domain.Interfaces;

namespace Sheba.Wallet.Application.Queries.GetMyCredentials;

public sealed class GetMyCredentialsHandler(IWalletRepository repository)
    : IRequestHandler<GetMyCredentialsQuery, List<CredentialDto>>
{
    public async Task<List<CredentialDto>> Handle(GetMyCredentialsQuery query, CancellationToken ct)
    {
        var creds = await repository.GetCredentialsBySubjectAsync(query.SubjectId, ct);

        return creds.Select(c => new CredentialDto(
            c.Id, c.CredentialType, c.IssuerDid, c.SubjectDid, c.Jwt,
            DecodeClaims(c.ClaimsJson),
            c.IssuedAt, c.ExpiresAt, c.IsRevoked
        )).ToList();
    }

    private static Dictionary<string, object>? DecodeClaims(string claimsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(claimsJson);
        }
        catch
        {
            return null;
        }
    }
}
