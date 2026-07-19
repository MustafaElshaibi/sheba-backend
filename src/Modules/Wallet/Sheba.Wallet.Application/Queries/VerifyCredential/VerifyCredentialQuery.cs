using MediatR;

namespace Sheba.Wallet.Application.Queries.VerifyCredential;

/// <summary>
/// Public verification of a presented VC-JWT (T-WAL-2, §5.6 "verification/presentation flow") —
/// no auth required. A relying party (ministry portal, another citizen, an external verifier)
/// presents a JWT they received from a citizen's wallet and gets back whether it's genuinely
/// Sheba-issued, unexpired, and unrevoked, plus its claims if valid.
/// </summary>
public sealed record VerifyCredentialQuery(string Jwt) : IRequest<VerifyCredentialResultDto>;

public sealed record VerifyCredentialResultDto(
    bool IsValid,
    string? Reason,
    Guid? CredentialId,
    string? CredentialType,
    string? IssuerDid,
    string? SubjectDid,
    Dictionary<string, object>? Claims,
    DateTime? IssuedAt,
    DateTime? ExpiresAt);
