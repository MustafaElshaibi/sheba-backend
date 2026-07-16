using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Admin.Application.Interfaces;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Admin.Application.Analytics.GetKpiSummary;

/// <summary>
/// Reads live platform KPIs by combining:
///   - IIdentityStatsProvider (cross-module: total accounts, pending requests)
///   - IAdminAnalyticsRepository (this module's read model: completions, SLA, approval times)
/// </summary>
public sealed class GetKpiSummaryHandler(
    IIdentityStatsProvider identityStats,
    IAdminAnalyticsRepository analyticsRepo,
    ILogger<GetKpiSummaryHandler> logger
) : IRequestHandler<GetKpiSummaryQuery, KpiSummaryDto>
{
    public async Task<KpiSummaryDto> Handle(GetKpiSummaryQuery request, CancellationToken ct)
    {
        var totalAccounts = await identityStats.GetTotalAccountsAsync(ct);
        var pendingRequests = await identityStats.GetPendingIdentityRequestsAsync(ct);
        var todayCompletions = await analyticsRepo.GetTodayCompletionsAsync(ct);
        var avgApprovalHours = await analyticsRepo.GetAvgApprovalHoursLast30DaysAsync(ct);
        var slaBreachCount = await analyticsRepo.GetSlaBreachCountLast30DaysAsync(ct);

        logger.LogDebug(
            "[GetKpiSummary] Accounts={Accounts} Pending={Pending} TodayCompletions={Completions}",
            totalAccounts, pendingRequests, todayCompletions);

        return new KpiSummaryDto(
            TotalAccounts: totalAccounts,
            PendingIdentityRequests: pendingRequests,
            TodayCompletions: todayCompletions,
            AvgApprovalHoursLast30Days: avgApprovalHours,
            SlaBreachCount: slaBreachCount);
    }
}
