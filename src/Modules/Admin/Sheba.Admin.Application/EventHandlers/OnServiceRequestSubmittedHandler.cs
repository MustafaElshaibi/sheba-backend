using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Admin.Application.Interfaces;
using Sheba.ServiceRequest.Domain.DomainEvents;

namespace Sheba.Admin.Application.EventHandlers;

/// <summary>
/// Cross-module handler: subscribes to ServiceRequestSubmittedEvent (from ServiceRequest module).
/// Increments today's DailyServiceRequestSnapshot submitted count.
/// </summary>
public sealed class OnServiceRequestSubmittedHandler(
    IAdminAnalyticsRepository analyticsRepo,
    ILogger<OnServiceRequestSubmittedHandler> logger
) : INotificationHandler<ServiceRequestSubmittedEvent>
{
    public async Task Handle(ServiceRequestSubmittedEvent notification, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // ServiceId is known; MinistryId is not in the event — use Guid.Empty as placeholder.
        // In a full production system the event would carry ministry_id.
        var snapshot = await analyticsRepo.GetOrCreateServiceRequestSnapshotAsync(
            today, notification.ServiceId, Guid.Empty, ct);

        snapshot.IncrementSubmitted();
        await analyticsRepo.SaveChangesAsync(ct);

        logger.LogInformation(
            "[AdminAnalytics] Service request {RequestId} submitted — snapshot updated for {Date}",
            notification.RequestId, today);
    }
}
