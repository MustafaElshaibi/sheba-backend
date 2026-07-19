using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Shared.Kernel.Events.IntegrationEvents;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Application.EventHandlers;

/// <summary>
/// Handles IdentityRequestDecidedEvent when Approved = false.
/// Sends a bilingual rejection notification email to the citizen using the
/// NotificationTemplate seeded by the Notification module (T-NOT-1).
/// </summary>
public sealed class SendRejectionEmailHandler(
    IIdentityRepository repository,
    IEmailService emailService,
    INotificationTemplateService templateService,
    IInboxGuard inboxGuard,
    ILogger<SendRejectionEmailHandler> logger
) : INotificationHandler<IdentityRequestDecidedEvent>
{
    private const string ConsumerName = nameof(SendRejectionEmailHandler);

    public async Task Handle(IdentityRequestDecidedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.Approved)
            return;

        if (await inboxGuard.IsProcessedAsync(notification.EventId, ConsumerName, cancellationToken))
            return;

        var account = await repository.FindAccountByIdAsync(notification.AccountId, cancellationToken);
        if (account is null)
        {
            logger.LogWarning(
                "[SendRejectionEmail] Account {AccountId} not found for rejected request {RequestId}",
                notification.AccountId, notification.RequestId);
            return;
        }

        if (string.IsNullOrWhiteSpace(account.Email))
        {
            logger.LogWarning(
                "[SendRejectionEmail] Account {AccountId} has no email — skipping notification.",
                notification.AccountId);
            return;
        }

        var reasonText = notification.RejectionReason ?? "Please contact support.";
        var reasonHtml = System.Net.WebUtility.HtmlEncode(reasonText);

        var rendered = await templateService.RenderAsync(
            WellKnownTemplateKeys.IdentityRequestRejected,
            new Dictionary<string, string>
            {
                ["ReasonHtml"] = reasonHtml,
                ["ReasonText"] = reasonText
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
                "[SendRejectionEmail] Rejection email sent to {Email} (AccountId={AccountId}, RequestId={RequestId})",
                account.Email, notification.AccountId, notification.RequestId);
        }
        else
            logger.LogError(
                "[SendRejectionEmail] Failed to send rejection email to {Email} (AccountId={AccountId})",
                account.Email, notification.AccountId);
    }
}
