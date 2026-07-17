using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.DomainEvents;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Application.EventHandlers;

/// <summary>
/// Handles IdentityRequestSubmittedEvent — notifies admin reviewers that a new
/// identity verification request is awaiting review.
///
/// Architecture notes:
///   - Triggered automatically when CompleteRegistrationHandler calls
///     SaveChangesAsync, which dispatches domain events via MediatR IPublisher.
///   - Uses IEmailService (registered by NotificationModule) to send the email.
///   - Uses IIdentityRepository to look up citizen account details for the email body.
///   - No direct DbContext access — fully decoupled from EF Core.
///
/// Production improvement: query the admin_users table for all IDENTITY_REVIEWER
/// emails and send to each. For graduation, we send to the configured admin address.
/// </summary>
public sealed class SendAdminNotificationHandler(
    IIdentityRepository repository,
    IEmailService emailService,
    IInboxGuard inboxGuard,
    ILogger<SendAdminNotificationHandler> logger
) : INotificationHandler<IdentityRequestSubmittedEvent>
{
    // In production this would come from IConfiguration or an admin query service.
    // Hardcoded admin email for graduation demonstration purposes.
    private const string AdminEmail   = "admin@sheba.gov";
    private const string AdminName    = "Sheba Identity Reviewer";
    private const string ConsumerName = nameof(SendAdminNotificationHandler);

    public async Task Handle(IdentityRequestSubmittedEvent notification, CancellationToken cancellationToken)
    {
        // Guarded by IInboxGuard (T-EVT-1): at-least-once outbox redelivery would otherwise
        // resend this admin notification email.
        if (await inboxGuard.IsProcessedAsync(notification.EventId, ConsumerName, cancellationToken))
            return;

        var account = await repository.FindAccountByIdAsync(notification.AccountId, cancellationToken);

        if (account is null)
        {
            logger.LogWarning(
                "[SendAdminNotification] Account {AccountId} not found for submitted request {RequestId}",
                notification.AccountId, notification.RequestId);
            return;
        }

        var htmlBody = $"""
            <h2>New Identity Verification Request</h2>
            <p>A new identity verification request has been submitted and requires your review.</p>
            <table border="1" cellpadding="8" cellspacing="0" style="border-collapse:collapse;">
              <tr><td><strong>Request ID</strong></td><td>{notification.RequestId}</td></tr>
              <tr><td><strong>Account ID</strong></td><td>{notification.AccountId}</td></tr>
              <tr><td><strong>Request Type</strong></td><td>{notification.RequestType}</td></tr>
              <tr><td><strong>Citizen Name (AR)</strong></td><td>{System.Net.WebUtility.HtmlEncode(account.FullNameAr)}</td></tr>
              <tr><td><strong>Citizen Name (EN)</strong></td><td>{System.Net.WebUtility.HtmlEncode(account.FullNameEn)}</td></tr>
              <tr><td><strong>Submitted At</strong></td><td>{notification.OccurredAt:u}</td></tr>
            </table>
            <p>Please log in to the Sheba Admin Portal to review and decide on this request.</p>
            <hr/>
            <p style="color:#888;font-size:12px;">
              This is an automated notification from the Sheba Identity Platform.
              Do not reply to this email.
            </p>
            """;

        var textBody =
            $"New identity request submitted for review. " +
            $"Request ID: {notification.RequestId} | " +
            $"Account: {account.FullNameEn} ({notification.AccountId}) | " +
            $"Type: {notification.RequestType} | " +
            $"Submitted: {notification.OccurredAt:u}. " +
            $"Please log in to the Sheba Admin Portal to review.";

        var sent = await emailService.SendAsync(
            toAddress:         AdminEmail,
            toName:            AdminName,
            subject:           $"🔔 New Identity Request Awaiting Review — {account.FullNameEn}",
            htmlBody:          htmlBody,
            textBody:          textBody,
            cancellationToken: cancellationToken);

        if (sent)
        {
            await inboxGuard.MarkProcessedAsync(notification.EventId, ConsumerName, cancellationToken);
            logger.LogInformation(
                "[SendAdminNotification] Admin notification sent for RequestId={RequestId} (AccountId={AccountId})",
                notification.RequestId, notification.AccountId);
        }
        else
            logger.LogError(
                "[SendAdminNotification] Failed to send admin notification for RequestId={RequestId}",
                notification.RequestId);
    }
}
