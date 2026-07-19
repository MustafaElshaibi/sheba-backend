using MediatR;

namespace Sheba.Wallet.Application.Queries.GetRevocationStatus;

/// <summary>
/// Public, lightweight revocation check by credential ID (T-WAL-2, BR-WA-2) — for a relying
/// party that already knows the credential's ID (e.g. embedded in a QR code alongside the JWT)
/// and wants a cheap status check without re-verifying the full JWT signature. Deliberately
/// leaks nothing beyond revocation status: no claims, no JWT.
/// </summary>
public sealed record GetRevocationStatusQuery(Guid CredentialId) : IRequest<RevocationStatusDto>;

public sealed record RevocationStatusDto(Guid CredentialId, bool IsRevoked, DateTime? RevokedAt);
