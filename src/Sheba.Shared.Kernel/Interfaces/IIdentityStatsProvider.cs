namespace Sheba.Shared.Kernel.Interfaces;

/// <summary>
/// Cross-module query interface for identity statistics.
///
/// Defined in Shared.Kernel so the Admin module can request live counts
/// without depending on Identity.Infrastructure or IdentityDbContext.
///
/// Implementation lives in Identity.Infrastructure (IdentityStatsAdapter).
/// Follows the same pattern as ICitizenAccountQueryService.
/// </summary>
public interface IIdentityStatsProvider
{
    /// <summary>Returns the total number of citizen accounts (all statuses).</summary>
    Task<int> GetTotalAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the count of identity requests with status = Pending.</summary>
    Task<int> GetPendingIdentityRequestsAsync(CancellationToken cancellationToken = default);
}
