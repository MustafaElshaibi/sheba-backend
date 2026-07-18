using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Shared.Kernel.Events.IntegrationEvents;
using Sheba.Shared.Kernel.Interfaces;
using Sheba.Wallet.Application.Commands.RevokeAccountCredentials;

namespace Sheba.Wallet.Application.EventHandlers;

/// <summary>Listens for AccountSuspendedEvent and revokes the account's VCs (BR-WA-1).</summary>
public sealed class RevokeCredentialsOnAccountSuspensionHandler(
    IMediator mediator,
    IInboxGuard inboxGuard,
    ILogger<RevokeCredentialsOnAccountSuspensionHandler> logger
) : INotificationHandler<AccountSuspendedEvent>
{
    private const string ConsumerName = nameof(RevokeCredentialsOnAccountSuspensionHandler);

    public async Task Handle(AccountSuspendedEvent notification, CancellationToken ct)
    {
        if (await inboxGuard.IsProcessedAsync(notification.EventId, ConsumerName, ct))
            return;

        try
        {
            var revoked = await mediator.Send(new RevokeAccountCredentialsCommand(notification.AccountId), ct);
            await inboxGuard.MarkProcessedAsync(notification.EventId, ConsumerName, ct);
            logger.LogInformation(
                "[Wallet] Account {AccountId} suspended — revoked {Count} credential(s)",
                notification.AccountId, revoked);
        }
        catch (Exception ex)
        {
            // Inbox deliberately NOT marked — a redelivered event retries the revocation.
            logger.LogError(ex, "[Wallet] Failed to revoke credentials for suspended account {AccountId}", notification.AccountId);
        }
    }
}
