using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Shared.Kernel.Events.IntegrationEvents;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Application.EventHandlers;

/// <summary>
/// Handles IdentityRequestDecidedEvent when Approved = true.
/// Sends a bilingual "welcome, your account is now active" email to the citizen using
/// the NotificationTemplate seeded by the Notification module (T-NOT-1).
/// </summary>
public sealed class SendApprovalEmailHandler(
    IIdentityRepository repository,
    IEmailService emailService,
    INotificationTemplateService templateService,
    IInboxGuard inboxGuard,
    ILogger<SendApprovalEmailHandler> logger
) : INotificationHandler<IdentityRequestDecidedEvent>
{
    private const string ConsumerName = nameof(SendApprovalEmailHandler);

    public async Task Handle(IdentityRequestDecidedEvent notification, CancellationToken cancellationToken)
    {
        if (!notification.Approved)
            return;

        if (await inboxGuard.IsProcessedAsync(notification.EventId, ConsumerName, cancellationToken))
            return;

        var account = await repository.FindAccountByIdAsync(notification.AccountId, cancellationToken);
        if (account is null)
        {
            logger.LogWarning(
                "[SendApprovalEmail] Account {AccountId} not found for approved request {RequestId}",
                notification.AccountId, notification.RequestId);
            return;
        }

        if (string.IsNullOrWhiteSpace(account.Email))
        {
            logger.LogWarning(
                "[SendApprovalEmail] Account {AccountId} has no email — skipping notification.",
                notification.AccountId);
            return;
        }

        var rendered = await templateService.RenderAsync(
            WellKnownTemplateKeys.IdentityRequestApproved,
            new Dictionary<string, string>
            {
                ["AccountId"] = notification.AccountId.ToString()
            },
            cancellationToken);

        var sent = await emailService.SendAsync(
            toAddress:         account.Email,
            toName:            account.FullNameEn ?? account.FullNameAr ?? "Citizen",
            subject:           rendered.Subject,
            htmlBody:          rendered.HtmlBody,
            textBody:          rendered.TextBody,
            cancellationToken: cancellationToken);

        if (sent)
        {
            await inboxGuard.MarkProcessedAsync(notification.EventId, ConsumerName, cancellationToken);
            logger.LogInformation(
                "[SendApprovalEmail] Approval email sent to {Email} (AccountId={AccountId}, RequestId={RequestId})",
                account.Email, notification.AccountId, notification.RequestId);
        }
        else
            logger.LogError(
                "[SendApprovalEmail] Failed to send approval email to {Email} (AccountId={AccountId})",
                account.Email, notification.AccountId);
    }
}
