using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.DomainEvents;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Application.EventHandlers;

/// <summary>
/// Handles IdentityRequestSubmittedEvent — notifies admin reviewers that a new
/// identity verification request is awaiting review.
/// Sends a bilingual email using the AdminNewIdentityRequest template (T-NOT-1).
/// </summary>
public sealed class SendAdminNotificationHandler(
    IIdentityRepository repository,
    IEmailService emailService,
    INotificationTemplateService templateService,
    IInboxGuard inboxGuard,
    ILogger<SendAdminNotificationHandler> logger
) : INotificationHandler<IdentityRequestSubmittedEvent>
{
    private const string AdminEmail   = "admin@sheba.gov";
    private const string AdminName    = "Sheba Identity Reviewer";
    private const string ConsumerName = nameof(SendAdminNotificationHandler);

    public async Task Handle(IdentityRequestSubmittedEvent notification, CancellationToken cancellationToken)
    {
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

        var nameAr = account.FullNameAr ?? "";
        var nameEn = account.FullNameEn ?? "";

        var rendered = await templateService.RenderAsync(
            WellKnownTemplateKeys.AdminNewIdentityRequest,
            new Dictionary<string, string>
            {
                ["RequestId"]     = notification.RequestId.ToString(),
                ["AccountId"]     = notification.AccountId.ToString(),
                ["RequestType"]   = notification.RequestType.ToString(),
                ["FullNameArHtml"] = System.Net.WebUtility.HtmlEncode(nameAr),
                ["FullNameEnHtml"] = System.Net.WebUtility.HtmlEncode(nameEn),
                ["FullNameArText"] = nameAr,
                ["FullNameEnText"] = nameEn,
                ["SubmittedAt"]   = notification.OccurredAt.ToString("u")
            },
            cancellationToken);

        var sent = await emailService.SendAsync(
            toAddress:         AdminEmail,
            toName:            AdminName,
            subject:           rendered.Subject,
            htmlBody:          rendered.HtmlBody,
            textBody:          rendered.TextBody,
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
