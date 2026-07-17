using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Queries.GetAccountById;
using Sheba.Shared.Kernel.Exceptions;
using Sheba.Wallet.Domain.Entities;
using Sheba.Wallet.Domain.Interfaces;

namespace Sheba.Wallet.Application.Commands.IssueIdentityCredential;

public sealed class IssueIdentityCredentialHandler(
    IMediator mediator,
    IWalletRepository repository,
    ICredentialSigner signer,
    ILogger<IssueIdentityCredentialHandler> logger
) : IRequestHandler<IssueIdentityCredentialCommand, IssueIdentityCredentialResponse>
{
    private const string CredentialType = "DigitalIdentityCredential";
    private static readonly TimeSpan Validity = TimeSpan.FromDays(365 * 5); // 5-year VC

    public async Task<IssueIdentityCredentialResponse> Handle(
        IssueIdentityCredentialCommand command, CancellationToken ct)
    {
        // Idempotency: don't re-issue if the citizen already has an active identity VC
        if (await repository.HasCredentialTypeAsync(command.AccountId, CredentialType, ct))
        {
            var existing = (await repository.GetCredentialsBySubjectAsync(command.AccountId, ct))
                .First(c => c.CredentialType == CredentialType && !c.IsRevoked);
            logger.LogInformation("[IssueIdentityCredential] Account {Id} already has an identity VC", command.AccountId);
            return new IssueIdentityCredentialResponse(
                existing.Id, existing.CredentialType, existing.SubjectDid, existing.Jwt,
                "Credential already issued.");
        }

        // Fetch verified claims from the Identity module (cross-module via MediatR query)
        var accountResult = await mediator.Send(new GetAccountByIdQuery(command.AccountId), ct);
        if (accountResult.IsFailure)
            throw new NotFoundException("Account", command.AccountId);
        var account = accountResult.Value;

        // Build + RSA-sign the VC-JWT
        var claims = new IdentityCredentialClaims(
            account.Id, account.MaskedNid, account.FullNameEn, account.FullNameAr, account.IdentityLevel);
        var signed = signer.SignIdentityCredential(claims, Validity);

        // Ensure the issuer DID document exists (created once)
        var issuerDid = await repository.GetIssuerDidAsync(ct);
        if (issuerDid is null)
        {
            issuerDid = DidDocument.Create(signer.IssuerDid, signer.IssuerPublicKeyPem);
            await repository.AddDidAsync(issuerDid, ct);
        }

        // Ensure the citizen's subject DID document exists
        var subjectDid = await repository.GetDidBySubjectAsync(account.Id, ct);
        if (subjectDid is null)
        {
            subjectDid = DidDocument.Create(signed.SubjectDid, signer.IssuerPublicKeyPem, account.Id);
            await repository.AddDidAsync(subjectDid, ct);
        }

        // Store the credential
        var vc = VerifiableCredential.Issue(
            account.Id, CredentialType, signed.IssuerDid, signed.SubjectDid,
            signed.Jwt, signed.ClaimsJson, signed.ExpiresAt);
        await repository.AddCredentialAsync(vc, ct);
        await repository.SaveChangesAsync(ct);

        logger.LogInformation("[IssueIdentityCredential] Issued {Type} for account {Id}", CredentialType, account.Id);

        return new IssueIdentityCredentialResponse(
            vc.Id, vc.CredentialType, vc.SubjectDid, vc.Jwt, "Digital Identity Credential issued.");
    }
}
