using MediatR;

namespace Sheba.Wallet.Application.Commands.RevokeAccountCredentials;

/// <summary>
/// Revokes every non-revoked VC issued to an account (BR-WA-1: suspension/deactivation revokes
/// the account's VCs). Idempotent — already-revoked credentials are skipped.
/// </summary>
public sealed record RevokeAccountCredentialsCommand(Guid AccountId) : IRequest<int>;
