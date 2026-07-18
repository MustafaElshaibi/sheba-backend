using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sheba.Ministry.Infrastructure.Persistence;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Ministry.Infrastructure.Adapters;

/// <summary>
/// Cross-module adapter that exposes ministry connectivity health without leaking
/// MinistryDbContext. Registered in MinistryModule.cs as IMinistryHealthProvider.
/// Consumed by the Admin module's ministry-health dashboard query.
/// </summary>
public sealed class MinistryHealthAdapter(
    MinistryDbContext db,
    ILogger<MinistryHealthAdapter> logger) : IMinistryHealthProvider
{
    public async Task<IReadOnlyList<MinistryHealthSnapshot>> GetHealthSnapshotsAsync(
        Guid? ministryId, CancellationToken cancellationToken = default)
    {
        var query = db.AuthConfigs.Where(c => c.IsActive).AsQueryable();
        if (ministryId.HasValue)
            query = query.Where(c => c.MinistryId == ministryId.Value);

        var configs = await query.ToListAsync(cancellationToken);

        // Projected in-memory, not via Select() on the query: AuthType's enum-to-string
        // conversion makes .ToString() unreliable to translate to SQL across providers.
        var snapshots = configs
            .Select(c => new MinistryHealthSnapshot(
                c.MinistryId,
                c.Id,
                c.Name,
                c.AuthType.ToString(),
                c.LastHealthCheckAt,
                c.LastHealthSuccess,
                c.LastHealthLatencyMs,
                c.LastHealthError))
            .ToList();

        logger.LogDebug(
            "[MinistryHealth] Returning {Count} health snapshot(s) (MinistryId={MinistryId})",
            snapshots.Count, ministryId);

        return snapshots;
    }
}
