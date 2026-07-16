using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Domain.DomainEvents;
using Sheba.Wallet.Application.Commands.IssueIdentityCredential;

namespace Sheba.Wallet.Application.EventHandlers;

/// <summary>
/// Listens for IdentityRequestDecidedEvent. When an identity request is APPROVED,
/// issues the citizen's W3C Digital Identity Credential.
/// Cross-module communication is via MediatR INotification (no direct DbContext access).
/// </summary>
public sealed class IssueCredentialOnApprovalHandler(
    IMediator mediator,
    ILogger<IssueCredentialOnApprovalHandler> logger
) : INotificationHandler<IdentityRequestDecidedEvent>
{
    public async Task Handle(IdentityRequestDecidedEvent notification, CancellationToken ct)
    {
        if (!notification.Approved)
            return;

        logger.LogInformation(
            "[Wallet] Identity request {RequestId} approved — issuing Digital Identity Credential for account {AccountId}",
            notification.RequestId, notification.AccountId);

        try
        {
            await mediator.Send(new IssueIdentityCredentialCommand(notification.AccountId), ct);
        }
        catch (Exception ex)
        {
            // Do not break the approval flow if VC issuance fails; log for retry/investigation
            logger.LogError(ex, "[Wallet] Failed to issue credential for account {AccountId}", notification.AccountId);
        }
    }
}
