using MediatR;

namespace Sheba.Admin.Application.Analytics.GetKpiSummary;

/// <summary>
/// Returns a snapshot of key platform indicators for the admin dashboard.
/// </summary>
/// <param name="MinistryId">
/// T-AUTH-3: narrows service-request-based figures to one ministry (a MinistryManager's
/// claim); null for SuperAdmin/Auditor sees the global aggregate. Registration-based figures
/// (TotalAccounts, PendingIdentityRequests, AvgApprovalHoursLast30Days) are never ministry-owned
/// and stay global regardless.
/// </param>
public sealed record GetKpiSummaryQuery(Guid? MinistryId = null) : IRequest<KpiSummaryDto>;

/// <summary>
/// Admin dashboard KPI snapshot.
/// </summary>
public sealed record KpiSummaryDto(
    int TotalAccounts,
    int PendingIdentityRequests,
    int TodayCompletions,
    decimal AvgApprovalHoursLast30Days,
    int SlaBreachCount
);
