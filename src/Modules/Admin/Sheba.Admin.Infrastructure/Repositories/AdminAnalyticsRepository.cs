using Microsoft.EntityFrameworkCore;
using Sheba.Admin.Application.Interfaces;
using Sheba.Admin.Domain.Entities;
using Sheba.Admin.Infrastructure.Persistence;

namespace Sheba.Admin.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of the admin analytics repository.
/// All queries target the admin_data schema — never production module databases.
/// </summary>
public sealed class AdminAnalyticsRepository(AdminDbContext db) : IAdminAnalyticsRepository
{
    // ── Registration Snapshots ───────────────────────────────────────────────

    public async Task<DailyRegistrationSnapshot?> GetRegistrationSnapshotAsync(
        DateOnly date, CancellationToken ct = default)
        => await db.DailyRegistrationSnapshots
            .FirstOrDefaultAsync(s => s.Date == date, ct);

    public async Task<DailyRegistrationSnapshot> GetOrCreateRegistrationSnapshotAsync(
        DateOnly date, CancellationToken ct = default)
    {
        var snapshot = await db.DailyRegistrationSnapshots
            .FirstOrDefaultAsync(s => s.Date == date, ct);

        if (snapshot is not null) return snapshot;

        snapshot = DailyRegistrationSnapshot.Create(date);
        await db.DailyRegistrationSnapshots.AddAsync(snapshot, ct);
        return snapshot;
    }

    public async Task<List<DailyRegistrationSnapshot>> GetRegistrationSnapshotsAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default)
        => await db.DailyRegistrationSnapshots
            .Where(s => s.Date >= from && s.Date <= to)
            .OrderBy(s => s.Date)
            .ToListAsync(ct);

    // ── Service Request Snapshots ────────────────────────────────────────────

    public async Task<DailyServiceRequestSnapshot?> GetServiceRequestSnapshotAsync(
        DateOnly date, Guid serviceId, CancellationToken ct = default)
        => await db.DailyServiceRequestSnapshots
            .FirstOrDefaultAsync(s => s.Date == date && s.ServiceId == serviceId, ct);

    public async Task<DailyServiceRequestSnapshot> GetOrCreateServiceRequestSnapshotAsync(
        DateOnly date, Guid serviceId, Guid ministryId, CancellationToken ct = default)
    {
        var snapshot = await db.DailyServiceRequestSnapshots
            .FirstOrDefaultAsync(s => s.Date == date && s.ServiceId == serviceId, ct);

        if (snapshot is not null) return snapshot;

        snapshot = DailyServiceRequestSnapshot.Create(date, serviceId, ministryId);
        await db.DailyServiceRequestSnapshots.AddAsync(snapshot, ct);
        return snapshot;
    }

    public async Task<List<DailyServiceRequestSnapshot>> GetServiceRequestSnapshotsAsync(
        DateOnly from, DateOnly to, Guid? ministryId = null, CancellationToken ct = default)
        => await db.DailyServiceRequestSnapshots
            .Where(s => s.Date >= from && s.Date <= to)
            .Where(s => ministryId == null || s.MinistryId == ministryId)
            .OrderBy(s => s.Date)
            .ToListAsync(ct);

    // ── Revenue Snapshots ─────────────────────────────────────────────────────

    public async Task<DailyRevenueSnapshot> GetOrCreateRevenueSnapshotAsync(
        DateOnly date, string currency, CancellationToken ct = default)
    {
        var snapshot = await db.DailyRevenueSnapshots
            .FirstOrDefaultAsync(s => s.Date == date && s.Currency == currency, ct);

        if (snapshot is not null) return snapshot;

        snapshot = DailyRevenueSnapshot.Create(date, currency);
        await db.DailyRevenueSnapshots.AddAsync(snapshot, ct);
        return snapshot;
    }

    // ── Aggregated Queries ──────────────────────────────────────────────────

    public async Task<decimal> GetTodayRevenueAsync(string currency = "YER", CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var snapshot = await db.DailyRevenueSnapshots
            .FirstOrDefaultAsync(s => s.Date == today && s.Currency == currency, ct);
        return snapshot?.TotalAmount ?? 0;
    }

    public async Task<int> GetTodayCompletionsAsync(Guid? ministryId = null, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await db.DailyServiceRequestSnapshots
            .Where(s => s.Date == today)
            .Where(s => ministryId == null || s.MinistryId == ministryId)
            .SumAsync(s => s.Completed, ct);
    }

    public async Task<decimal> GetAvgApprovalHoursLast30DaysAsync(CancellationToken ct = default)
    {
        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var snapshots = await db.DailyRegistrationSnapshots
            .Where(s => s.Date >= from && s.AvgApprovalHours != null)
            .ToListAsync(ct);

        if (snapshots.Count == 0) return 0;
        return snapshots.Average(s => s.AvgApprovalHours!.Value);
    }

    public async Task<int> GetSlaBreachCountLast30DaysAsync(Guid? ministryId = null, CancellationToken ct = default)
    {
        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        return await db.DailyServiceRequestSnapshots
            .Where(s => s.Date >= from)
            .Where(s => ministryId == null || s.MinistryId == ministryId)
            .SumAsync(s => s.SlaBreach, ct);
    }

    // ── Report Jobs ──────────────────────────────────────────────────────────

    public async Task<ReportJob> AddReportJobAsync(ReportJob job, CancellationToken ct = default)
    {
        await db.ReportJobs.AddAsync(job, ct);
        return job;
    }

    public async Task<ReportJob?> GetReportJobAsync(Guid id, CancellationToken ct = default)
        => await db.ReportJobs.FindAsync([id], ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
