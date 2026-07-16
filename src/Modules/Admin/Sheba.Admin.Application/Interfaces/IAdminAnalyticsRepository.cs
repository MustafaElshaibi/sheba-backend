using Sheba.Admin.Domain.Entities;

namespace Sheba.Admin.Application.Interfaces;

/// <summary>
/// Read-only repository for admin analytics snapshots.
/// Implementation lives in Admin.Infrastructure (backed by AdminDbContext).
/// </summary>
public interface IAdminAnalyticsRepository
{
    // ── Registration Snapshots ───────────────────────────────────────────────
    Task<DailyRegistrationSnapshot?> GetRegistrationSnapshotAsync(DateOnly date, CancellationToken ct = default);
    Task<DailyRegistrationSnapshot> GetOrCreateRegistrationSnapshotAsync(DateOnly date, CancellationToken ct = default);
    Task<List<DailyRegistrationSnapshot>> GetRegistrationSnapshotsAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    // ── Service Request Snapshots ────────────────────────────────────────────
    Task<DailyServiceRequestSnapshot?> GetServiceRequestSnapshotAsync(DateOnly date, Guid serviceId, CancellationToken ct = default);
    Task<DailyServiceRequestSnapshot> GetOrCreateServiceRequestSnapshotAsync(DateOnly date, Guid serviceId, Guid ministryId, CancellationToken ct = default);
    Task<List<DailyServiceRequestSnapshot>> GetServiceRequestSnapshotsAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    // ── Aggregated Queries ──────────────────────────────────────────────────
    Task<int> GetTodayCompletionsAsync(CancellationToken ct = default);
    Task<decimal> GetAvgApprovalHoursLast30DaysAsync(CancellationToken ct = default);
    Task<int> GetSlaBreachCountLast30DaysAsync(CancellationToken ct = default);

    // ── Report Jobs ──────────────────────────────────────────────────────────
    Task<ReportJob> AddReportJobAsync(ReportJob job, CancellationToken ct = default);
    Task<ReportJob?> GetReportJobAsync(Guid id, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
