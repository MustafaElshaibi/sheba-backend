namespace Sheba.Shared.Kernel.Interfaces;

/// <summary>
/// Cross-module query interface for ministry connectivity health.
///
/// Defined in Shared.Kernel so the Admin module can surface the health dashboard without
/// depending on Ministry.Infrastructure or MinistryDbContext.
///
/// Implementation lives in Ministry.Infrastructure (MinistryHealthAdapter), fed by
/// MinistryHealthSweepJob's scheduled TestConnectionAsync sweep. Follows the same pattern as
/// IIdentityStatsProvider/ICitizenAccountQueryService.
/// </summary>
public interface IMinistryHealthProvider
{
    /// <summary>
    /// Latest recorded health status for every active ministry auth config.
    /// <paramref name="ministryId"/> narrows to one ministry (T-AUTH-3 — a MinistryManager's
    /// claim); null returns every ministry's configs (SuperAdmin/Auditor).
    /// </summary>
    Task<IReadOnlyList<MinistryHealthSnapshot>> GetHealthSnapshotsAsync(
        Guid? ministryId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only cross-module DTO for one auth config's last recorded connectivity check.
/// <c>LastCheckedAt</c>/<c>IsHealthy</c> are null when the config has never been swept yet.
/// </summary>
public sealed record MinistryHealthSnapshot(
    Guid MinistryId,
    Guid AuthConfigId,
    string AuthConfigName,
    string AuthType,
    DateTime? LastCheckedAt,
    bool? IsHealthy,
    long? LatencyMs,
    string? Error);
