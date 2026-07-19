using MediatR;
using Sheba.Wallet.Application.Queries.GetMyCredentials;

namespace Sheba.Wallet.Application.Queries.GetCredentialById;

/// <summary>Single-credential detail (JWT + decoded claims), ownership-checked like the rest of
/// the citizen-facing wallet surface.</summary>
public sealed record GetCredentialByIdQuery(Guid CredentialId, Guid ActorId, bool IsAdmin) : IRequest<CredentialDto>;
