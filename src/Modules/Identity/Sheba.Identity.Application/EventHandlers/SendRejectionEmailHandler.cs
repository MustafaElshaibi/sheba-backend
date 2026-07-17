using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.DomainEvents;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Application.EventHandlers;

/// <summary>
/// Handles IdentityRequestDecidedEvent when Approved = false.
/// Sends a rejection notification email to the citizen.
///
/// Uses IIdentityRepository to look up the account email.
/// Uses IEmailService (registered by NotificationModule) to dispatch the email.
/// No direct DbContext — fully decoupled from EF Core.
/// </summary>
public sealed class SendRejectionEmailHandler(
    IIdentityRepository repository,
    IEmailService emailService,
    IInboxGuard inboxGuard,
    ILogger<SendRejectionEmailHandler> logger
) : INotificationHandler<IdentityRequestDecidedEvent>
{
    private const string ConsumerName = nameof(SendRejectionEmailHandler);

    public async Task Handle(IdentityRequestDecidedEvent notification, CancellationToken cancellationToken)
    {
        // Only handle rejections
        if (notification.Approved)
            return;

        // Guarded by IInboxGuard (T-EVT-1): at-least-once outbox redelivery would otherwise
        // resend this rejection email.
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

        // Decide whether to include the reason (some reasons are internal-only)
        var citizenReason = notification.RejectionReason is not null
            ? $"<p><strong>Reason:</strong> {System.Net.WebUtility.HtmlEncode(notification.RejectionReason)}</p>"
            : "<p>If you believe this is an error, please contact support.</p>";

        var htmlBody =
            $"""
            <h2>Identity Verification Update</h2>
            <p>Dear citizen,</p>
            <p>We regret to inform you that your identity verification request has <strong>not been approved</strong>.</p>
            {citizenReason}
            <p>You may submit a new request after correcting the identified issues.</p>
            <p>For assistance, contact <a href="mailto:support@sheba.gov">support@sheba.gov</a>.</p>
            <hr/>
            <p style="color:#888;font-size:12px;">
              This is an automated message from the Sheba Identity Platform.
              Please do not reply to this email.
            </p>
            """;

        var textBody =
            "Your Sheba identity verification request was not approved. " +
            (notification.RejectionReason ?? "Please contact support for details.");

        var sent = await emailService.SendAsync(
            toAddress:   account.Email,
            toName:      account.FullNameEn ?? account.FullNameAr ?? "Citizen",
            subject:     "❌ Sheba identity verification — action required",
            htmlBody:    htmlBody,
            textBody:    textBody,
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
