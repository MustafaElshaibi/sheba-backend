using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Admin.Application.Interfaces;
using Sheba.Shared.Kernel.Events.IntegrationEvents;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Admin.Application.EventHandlers;

/// <summary>
/// Cross-module handler: subscribes to PaymentCompletedEvent (from the Payment module, T-PAY-1).
/// Increments today's DailyRevenueSnapshot for the order's currency.
///
/// Guarded by IInboxGuard (T-EVT-1): at-least-once outbox redelivery would otherwise double-count
/// this snapshot increment.
/// </summary>
public sealed class OnPaymentCompletedHandler(
    IAdminAnalyticsRepository analyticsRepo,
    IInboxGuard inboxGuard,
    ILogger<OnPaymentCompletedHandler> logger
) : INotificationHandler<PaymentCompletedEvent>
{
    private const string ConsumerName = nameof(OnPaymentCompletedHandler);

    public async Task Handle(PaymentCompletedEvent notification, CancellationToken ct)
    {
        if (await inboxGuard.IsProcessedAsync(notification.EventId, ConsumerName, ct))
            return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var snapshot = await analyticsRepo.GetOrCreateRevenueSnapshotAsync(today, notification.Currency, ct);
        snapshot.RecordPayment(notification.Amount);

        await analyticsRepo.SaveChangesAsync(ct);
        await inboxGuard.MarkProcessedAsync(notification.EventId, ConsumerName, ct);

        logger.LogInformation(
            "[AdminAnalytics] Payment order {OrderId} recorded ({Amount} {Currency}) — revenue snapshot updated for {Date}",
            notification.PaymentOrderId, notification.Amount, notification.Currency, today);
    }
}
