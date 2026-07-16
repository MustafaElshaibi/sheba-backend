using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Infrastructure.Persistence;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Infrastructure.Adapters;

/// <summary>
/// Cross-module adapter that exposes identity statistics without leaking IdentityDbContext.
///
/// Registered in IdentityModule.cs as IIdentityStatsProvider.
/// Consumed by Admin module's GetKpiSummaryHandler for live dashboard KPIs.
/// </summary>
public sealed class IdentityStatsAdapter(
    IdentityDbContext db,
    ILogger<IdentityStatsAdapter> logger) : IIdentityStatsProvider
{
    public async Task<int> GetTotalAccountsAsync(CancellationToken cancellationToken = default)
    {
        var count = await db.Accounts.CountAsync(cancellationToken);
        logger.LogDebug("[IdentityStats] Total accounts: {Count}", count);
        return count;
    }

    public async Task<int> GetPendingIdentityRequestsAsync(CancellationToken cancellationToken = default)
    {
        var count = await db.IdentityRequests
            .CountAsync(r => r.Status == RequestStatus.Pending
                          || r.Status == RequestStatus.UnderReview,
                cancellationToken);
        logger.LogDebug("[IdentityStats] Pending identity requests: {Count}", count);
        return count;
    }
}
