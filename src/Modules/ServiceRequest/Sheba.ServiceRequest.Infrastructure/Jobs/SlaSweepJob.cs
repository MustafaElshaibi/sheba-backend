using Microsoft.Extensions.Logging;
using Sheba.ServiceRequest.Domain.Interfaces;

namespace Sheba.ServiceRequest.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job (T-SRV-3 / BR-SR-6): expires AwaitingMinistry requests past their
/// DueDate — an abandoned request otherwise sits open forever.
///
/// Registered in Program.cs:
///   RecurringJob.AddOrUpdate&lt;SlaSweepJob&gt;(
///       "sla-sweep", job => job.SweepAsync(CancellationToken.None), Cron.Hourly());
/// </summary>
public sealed class SlaSweepJob(
    IServiceRequestRepository repository,
    ILogger<SlaSweepJob> logger)
{
    public async Task SweepAsync(CancellationToken ct)
    {
        var overdue = await repository.GetOverdueAwaitingMinistryRequestsAsync(DateTime.UtcNow, ct);
        foreach (var request in overdue)
        {
            logger.LogInformation(
                "[SlaSweep] Expiring overdue request {Ref} (DueDate={DueDate})",
                request.ReferenceNumber, request.DueDate);
            request.Expire();
        }

        if (overdue.Count > 0)
            await repository.SaveChangesAsync(ct);

        logger.LogInformation("[SlaSweep] Expired {Count} overdue request(s).", overdue.Count);
    }
}
