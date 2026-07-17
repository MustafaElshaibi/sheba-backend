using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Admin.Application.Interfaces;
using Sheba.Shared.Kernel.Events.IntegrationEvents;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Admin.Application.EventHandlers;

/// <summary>
/// Cross-module handler: subscribes to ServiceRequestSubmittedEvent (from ServiceRequest module).
/// Increments today's DailyServiceRequestSnapshot submitted count.
///
/// Guarded by IInboxGuard (T-EVT-1): at-least-once outbox redelivery would otherwise double-count
/// this snapshot increment.
/// </summary>
public sealed class OnServiceRequestSubmittedHandler(
    IAdminAnalyticsRepository analyticsRepo,
    IInboxGuard inboxGuard,
    ILogger<OnServiceRequestSubmittedHandler> logger
) : INotificationHandler<ServiceRequestSubmittedEvent>
{
    private const string ConsumerName = nameof(OnServiceRequestSubmittedHandler);

    public async Task Handle(ServiceRequestSubmittedEvent notification, CancellationToken ct)
    {
        if (await inboxGuard.IsProcessedAsync(notification.EventId, ConsumerName, ct))
            return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // ServiceId is known; MinistryId is not in the event — use Guid.Empty as placeholder.
        // In a full production system the event would carry ministry_id.
        var snapshot = await analyticsRepo.GetOrCreateServiceRequestSnapshotAsync(
            today, notification.ServiceId, Guid.Empty, ct);

        snapshot.IncrementSubmitted();
        await analyticsRepo.SaveChangesAsync(ct);
        await inboxGuard.MarkProcessedAsync(notification.EventId, ConsumerName, ct);

        logger.LogInformation(
            "[AdminAnalytics] Service request {RequestId} submitted — snapshot updated for {Date}",
            notification.RequestId, today);
    }
}
