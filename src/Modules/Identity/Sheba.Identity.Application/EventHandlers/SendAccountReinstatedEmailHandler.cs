using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Shared.Kernel.Events.IntegrationEvents;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Application.EventHandlers;

/// <summary>
/// Notifies the citizen by email when an admin lifts a security hold on their account.
/// </summary>
public sealed class SendAccountReinstatedEmailHandler(
    IIdentityRepository repository,
    IEmailService emailService,
    IInboxGuard inboxGuard,
    ILogger<SendAccountReinstatedEmailHandler> logger
) : INotificationHandler<AccountReinstatedEvent>
{
    private const string ConsumerName = nameof(SendAccountReinstatedEmailHandler);

    public async Task Handle(AccountReinstatedEvent notification, CancellationToken cancellationToken)
    {
        if (await inboxGuard.IsProcessedAsync(notification.EventId, ConsumerName, cancellationToken))
            return;

        var account = await repository.FindAccountByIdAsync(notification.AccountId, cancellationToken);
        if (account is null || string.IsNullOrWhiteSpace(account.Email))
            return;

        var sent = await emailService.SendAsync(
            toAddress:  account.Email,
            toName:     account.FullNameEn ?? account.FullNameAr ?? "Citizen",
            subject:    "✅ Sheba account reinstated",
            htmlBody:
                """
                <h2>Account Reinstated</h2>
                <p>Dear citizen,</p>
                <p>Your Sheba account has been <strong>reinstated</strong>. You can now log in as usual.</p>
                """,
            textBody:   "Your Sheba account has been reinstated. You can now log in as usual.",
            cancellationToken: cancellationToken);

        if (sent)
            await inboxGuard.MarkProcessedAsync(notification.EventId, ConsumerName, cancellationToken);
        else
            logger.LogError("[SendAccountReinstatedEmail] Failed to send email for AccountId={AccountId}", account.Id);
    }
}
