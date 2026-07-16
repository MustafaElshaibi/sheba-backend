using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Admin.Application.Interfaces;
using Sheba.ServiceRequest.Domain.DomainEvents;

namespace Sheba.Admin.Application.EventHandlers;

/// <summary>
/// Cross-module handler: subscribes to ServiceRequestCompletedEvent (from ServiceRequest module).
/// Increments today's DailyServiceRequestSnapshot completed count and calculates processing time.
/// </summary>
public sealed class OnServiceRequestCompletedHandler(
    IAdminAnalyticsRepository analyticsRepo,
    ILogger<OnServiceRequestCompletedHandler> logger
) : INotificationHandler<ServiceRequestCompletedEvent>
{
    public async Task Handle(ServiceRequestCompletedEvent notification, CancellationToken ct)
    {
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

        logger.LogInformation(
            "[AdminAnalytics] Service request {RequestId} completed ({Hours:F1}h) — snapshot updated for {Date}",
            notification.RequestId, processingHours, today);
    }
}
