using MediatR;

namespace Sheba.Wallet.Application.Commands.IssueIdentityCredential;

/// <summary>
/// Issues a W3C "DigitalIdentityCredential" for an approved citizen account.
/// Triggered by the Wallet event handler on IdentityRequestDecidedEvent (approved),
/// or callable directly for testing.
/// </summary>
public sealed record IssueIdentityCredentialCommand(Guid AccountId)
    : IRequest<IssueIdentityCredentialResponse>;

public sealed record IssueIdentityCredentialResponse(
    Guid CredentialId,
    string CredentialType,
    string SubjectDid,
    string Jwt,
    string Message);
