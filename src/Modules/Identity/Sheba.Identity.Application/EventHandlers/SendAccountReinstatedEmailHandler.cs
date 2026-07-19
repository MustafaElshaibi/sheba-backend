using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Shared.Kernel.Events.IntegrationEvents;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Application.EventHandlers;

/// <summary>
/// Notifies the citizen by email when an admin lifts a security hold on their account.
/// Sends a bilingual email using the AccountReinstated template (T-NOT-1).
/// </summary>
public sealed class SendAccountReinstatedEmailHandler(
    IIdentityRepository repository,
    IEmailService emailService,
    INotificationTemplateService templateService,
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

        var rendered = await templateService.RenderAsync(
            WellKnownTemplateKeys.AccountReinstated,
            new Dictionary<string, string>(),
            cancellationToken);

        var sent = await emailService.SendAsync(
            toAddress:         account.Email,
            toName:            account.FullNameEn ?? account.FullNameAr ?? "Citizen",
            subject:           rendered.Subject,
            htmlBody:          rendered.HtmlBody,
            textBody:          rendered.TextBody,
            cancellationToken: cancellationToken);

        if (sent)
            await inboxGuard.MarkProcessedAsync(notification.EventId, ConsumerName, cancellationToken);
        else
            logger.LogError("[SendAccountReinstatedEmail] Failed to send email for AccountId={AccountId}", account.Id);
    }
}
