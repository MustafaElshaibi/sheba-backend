using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Shared.Kernel.Events.IntegrationEvents;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Application.EventHandlers;

/// <summary>
/// Notifies the citizen by email when their account is deactivated (terminal).
/// Sends a bilingual email using the AccountDeactivated template (T-NOT-1).
/// </summary>
public sealed class SendAccountDeactivatedEmailHandler(
    IIdentityRepository repository,
    IEmailService emailService,
    INotificationTemplateService templateService,
    IInboxGuard inboxGuard,
    ILogger<SendAccountDeactivatedEmailHandler> logger
) : INotificationHandler<AccountDeactivatedEvent>
{
    private const string ConsumerName = nameof(SendAccountDeactivatedEmailHandler);

    public async Task Handle(AccountDeactivatedEvent notification, CancellationToken cancellationToken)
    {
        if (await inboxGuard.IsProcessedAsync(notification.EventId, ConsumerName, cancellationToken))
            return;

        var account = await repository.FindAccountByIdAsync(notification.AccountId, cancellationToken);
        if (account is null || string.IsNullOrWhiteSpace(account.Email))
            return;

        var reasonText = notification.Reason ?? "";
        var rendered = await templateService.RenderAsync(
            WellKnownTemplateKeys.AccountDeactivated,
            new Dictionary<string, string>
            {
                ["ReasonHtml"] = System.Net.WebUtility.HtmlEncode(reasonText),
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
            await inboxGuard.MarkProcessedAsync(notification.EventId, ConsumerName, cancellationToken);
        else
            logger.LogError("[SendAccountDeactivatedEmail] Failed to send email for AccountId={AccountId}", account.Id);
    }
}
