using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Admin.Application.Interfaces;
using Sheba.ServiceRequest.Domain.DomainEvents;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Admin.Application.EventHandlers;

/// <summary>
/// Cross-module handler: subscribes to ServiceRequestCompletedEvent (from ServiceRequest module).
/// Increments today's DailyServiceRequestSnapshot completed count and calculates processing time.
///
/// Guarded by IInboxGuard (T-EVT-1): at-least-once outbox redelivery would otherwise double-count
/// this snapshot increment.
/// </summary>
public sealed class OnServiceRequestCompletedHandler(
    IAdminAnalyticsRepository analyticsRepo,
    IInboxGuard inboxGuard,
    ILogger<OnServiceRequestCompletedHandler> logger
) : INotificationHandler<ServiceRequestCompletedEvent>
{
    private const string ConsumerName = nameof(OnServiceRequestCompletedHandler);

    public async Task Handle(ServiceRequestCompletedEvent notification, CancellationToken ct)
    {
        if (await inboxGuard.IsProcessedAsync(notification.EventId, ConsumerName, ct))
            return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var snapshot = await analyticsRepo.GetOrCreateServiceRequestSnapshotAsync(
            today, notification.ServiceId, Guid.Empty, ct);

        snapshot.IncrementCompleted();

        // Calculate processing time and update average
        var processingHours = (decimal)(notification.CompletedAt - notification.SubmittedAt).TotalHours;
        if (snapshot.AvgCompletionHours.HasValue)
        {
            // Running average: ((old_avg * (n-1)) + new_value) / n
            var n = snapshot.Completed;
            var newAvg = ((snapshot.AvgCompletionHours.Value * (n - 1)) + processingHours) / n;
            snapshot.SetAvgCompletionHours(newAvg);
        }
        else
        {
            snapshot.SetAvgCompletionHours(processingHours);
        }

        await analyticsRepo.SaveChangesAsync(ct);
        await inboxGuard.MarkProcessedAsync(notification.EventId, ConsumerName, ct);

        logger.LogInformation(
            "[AdminAnalytics] Service request {RequestId} completed ({Hours:F1}h) — snapshot updated for {Date}",
            notification.RequestId, processingHours, today);
    }
}
