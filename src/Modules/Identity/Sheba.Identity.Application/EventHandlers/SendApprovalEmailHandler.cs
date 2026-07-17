using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Shared.Kernel.Events.IntegrationEvents;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Application.EventHandlers;

/// <summary>
/// Handles IdentityRequestDecidedEvent when Approved = true.
/// Sends a "welcome, your account is now active" email to the citizen.
///
/// Uses IIdentityRepository to look up the account email.
/// Uses IEmailService (registered by NotificationModule) to dispatch the email.
/// No direct DbContext — fully decoupled from EF Core.
/// </summary>
public sealed class SendApprovalEmailHandler(
    IIdentityRepository repository,
    IEmailService emailService,
    IInboxGuard inboxGuard,
    ILogger<SendApprovalEmailHandler> logger
) : INotificationHandler<IdentityRequestDecidedEvent>
{
    private const string ConsumerName = nameof(SendApprovalEmailHandler);

    public async Task Handle(IdentityRequestDecidedEvent notification, CancellationToken cancellationToken)
    {
        // Only handle approved decisions
        if (!notification.Approved)
            return;

        // Guarded by IInboxGuard (T-EVT-1): at-least-once outbox redelivery would otherwise
        // resend this approval email.
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

        var htmlBody =
            $"""
            <h2>Welcome to Sheba Digital Services!</h2>
            <p>Dear citizen,</p>
            <p>Your identity verification request has been <strong>approved</strong>.</p>
            <p>You can now log in and access all Sheba e-Government services.</p>
            <p>Account ID: {notification.AccountId}</p>
            <hr/>
            <p style="color:#888;font-size:12px;">
              This is an automated message from the Sheba Identity Platform.
              Please do not reply to this email.
            </p>
            """;

        var textBody =
            $"Your Sheba identity verification has been approved. " +
            $"You can now log in and access all e-Government services. " +
            $"Account ID: {notification.AccountId}";

        var sent = await emailService.SendAsync(
            toAddress:   account.Email,
            toName:      account.FullNameEn ?? account.FullNameAr ?? "Citizen",
            subject:     "✅ Your Sheba account is now active",
            htmlBody:    htmlBody,
            textBody:    textBody,
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
