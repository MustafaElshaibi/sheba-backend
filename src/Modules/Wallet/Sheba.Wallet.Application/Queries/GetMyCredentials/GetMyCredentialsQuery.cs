using MediatR;

namespace Sheba.Wallet.Application.Queries.GetMyCredentials;

public sealed record GetMyCredentialsQuery(Guid SubjectId) : IRequest<List<CredentialDto>>;

public sealed record CredentialDto(
    Guid Id,
    string CredentialType,
    string IssuerDid,
    string SubjectDid,
    string Jwt,
    Dictionary<string, object>? Claims,
    DateTime IssuedAt,
    DateTime? ExpiresAt,
    bool IsRevoked);
