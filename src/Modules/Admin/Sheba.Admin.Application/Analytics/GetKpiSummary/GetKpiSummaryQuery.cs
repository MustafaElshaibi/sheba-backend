using MediatR;

namespace Sheba.Admin.Application.Analytics.GetKpiSummary;

/// <summary>
/// Returns a snapshot of key platform indicators for the admin dashboard.
/// </summary>
public sealed record GetKpiSummaryQuery : IRequest<KpiSummaryDto>;

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
