using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Admin.Application.Interfaces;
using Sheba.Identity.Domain.DomainEvents;

namespace Sheba.Admin.Application.EventHandlers;

/// <summary>
/// Cross-module handler: subscribes to IdentityRequestDecidedEvent (from Identity module).
/// Updates today's DailyRegistrationSnapshot — increments approved or rejected count.
/// </summary>
public sealed class OnIdentityRequestDecidedHandler(
    IAdminAnalyticsRepository analyticsRepo,
    ILogger<OnIdentityRequestDecidedHandler> logger
) : INotificationHandler<IdentityRequestDecidedEvent>
{
    public async Task Handle(IdentityRequestDecidedEvent notification, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var snapshot = await analyticsRepo.GetOrCreateRegistrationSnapshotAsync(today, ct);

        if (notification.Approved)
            snapshot.IncrementApproved();
        else
            snapshot.IncrementRejected();

        await analyticsRepo.SaveChangesAsync(ct);

        logger.LogInformation(
            "[AdminAnalytics] Identity request {RequestId} {Decision} — snapshot updated for {Date}",
            notification.RequestId,
            notification.Approved ? "APPROVED" : "REJECTED",
            today);
    }
}
