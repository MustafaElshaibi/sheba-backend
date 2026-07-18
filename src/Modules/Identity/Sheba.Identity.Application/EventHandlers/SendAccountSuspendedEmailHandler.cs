using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Shared.Kernel.Events.IntegrationEvents;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Application.EventHandlers;

/// <summary>
/// Notifies the citizen by email when an admin places a security hold on their account.
/// </summary>
public sealed class SendAccountSuspendedEmailHandler(
    IIdentityRepository repository,
    IEmailService emailService,
    IInboxGuard inboxGuard,
    ILogger<SendAccountSuspendedEmailHandler> logger
) : INotificationHandler<AccountSuspendedEvent>
{
    private const string ConsumerName = nameof(SendAccountSuspendedEmailHandler);

    public async Task Handle(AccountSuspendedEvent notification, CancellationToken cancellationToken)
    {
        if (await inboxGuard.IsProcessedAsync(notification.EventId, ConsumerName, cancellationToken))
            return;

        var account = await repository.FindAccountByIdAsync(notification.AccountId, cancellationToken);
        if (account is null || string.IsNullOrWhiteSpace(account.Email))
            return;

        var reasonHtml = notification.Reason is not null
            ? $"<p><strong>Reason:</strong> {System.Net.WebUtility.HtmlEncode(notification.Reason)}</p>"
            : "<p>Contact support for details.</p>";

        var sent = await emailService.SendAsync(
            toAddress:  account.Email,
            toName:     account.FullNameEn ?? account.FullNameAr ?? "Citizen",
            subject:    "⚠️ Sheba account suspended",
            htmlBody:
                $"""
                <h2>Account Suspended</h2>
                <p>Dear citizen,</p>
                <p>Your Sheba account has been <strong>suspended</strong> and you will not be able to log in until it is reinstated.</p>
                {reasonHtml}
                <p>For assistance, contact <a href="mailto:support@sheba.gov">support@sheba.gov</a>.</p>
                """,
            textBody:   "Your Sheba account has been suspended. " + (notification.Reason ?? "Contact support for details."),
            cancellationToken: cancellationToken);

        if (sent)
            await inboxGuard.MarkProcessedAsync(notification.EventId, ConsumerName, cancellationToken);
        else
            logger.LogError("[SendAccountSuspendedEmail] Failed to send email for AccountId={AccountId}", account.Id);
    }
}
