using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Shared.Kernel.Events.IntegrationEvents;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Wallet.Application.Commands.IssueIdentityCredential;

namespace Sheba.Wallet.Application.EventHandlers;

/// <summary>
/// Listens for IdentityRequestDecidedEvent. When an identity request is APPROVED,
/// issues the citizen's W3C Digital Identity Credential.
/// Cross-module communication is via MediatR INotification (no direct DbContext access).
///
/// Guarded by IInboxGuard (T-EVT-1): the outbox dispatcher delivers at-least-once, so without
/// this check a redelivered event would attempt to issue a second credential.
/// </summary>
public sealed class IssueCredentialOnApprovalHandler(
    IMediator mediator,
    IInboxGuard inboxGuard,
    ILogger<IssueCredentialOnApprovalHandler> logger
) : INotificationHandler<IdentityRequestDecidedEvent>
{
    private const string ConsumerName = nameof(IssueCredentialOnApprovalHandler);

    public async Task Handle(IdentityRequestDecidedEvent notification, CancellationToken ct)
    {
        if (!notification.Approved)
            return;

        if (await inboxGuard.IsProcessedAsync(notification.EventId, ConsumerName, ct))
            return;

        logger.LogInformation(
            "[Wallet] Identity request {RequestId} approved — issuing Digital Identity Credential for account {AccountId}",
            notification.RequestId, notification.AccountId);

        try
        {
            await mediator.Send(new IssueIdentityCredentialCommand(notification.AccountId), ct);
            await inboxGuard.MarkProcessedAsync(notification.EventId, ConsumerName, ct);
        }
        catch (Exception ex)
        {
            // Do not break the approval flow if VC issuance fails; log for retry/investigation.
            // Inbox is deliberately NOT marked here, so a redelivered event retries the issuance.
            logger.LogError(ex, "[Wallet] Failed to issue credential for account {AccountId}", notification.AccountId);
        }
    }
}
