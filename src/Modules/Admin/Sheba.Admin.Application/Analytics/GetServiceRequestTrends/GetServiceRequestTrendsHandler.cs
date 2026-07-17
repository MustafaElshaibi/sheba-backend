using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Admin.Application.Interfaces;

namespace Sheba.Admin.Application.Analytics.GetServiceRequestTrends;

/// <summary>
/// Reads daily service request snapshots for the last N days.
/// Aggregates across all services. Missing days filled with zeros.
/// </summary>
public sealed class GetServiceRequestTrendsHandler(
    IAdminAnalyticsRepository analyticsRepo,
    ILogger<GetServiceRequestTrendsHandler> logger
) : IRequestHandler<GetServiceRequestTrendsQuery, List<ServiceTrendPointDto>>
{
    public async Task<List<ServiceTrendPointDto>> Handle(GetServiceRequestTrendsQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = today.AddDays(-(request.Days - 1));

        var snapshots = await analyticsRepo.GetServiceRequestSnapshotsAsync(from, today, request.MinistryId, ct);

        // Group by date and aggregate across all services
        var grouped = snapshots
            .GroupBy(s => s.Date)
            .ToDictionary(
                g => g.Key,
                g => (Submitted: g.Sum(s => s.Submitted), Completed: g.Sum(s => s.Completed)));

        var result = new List<ServiceTrendPointDto>(request.Days);
        for (var d = from; d <= today; d = d.AddDays(1))
        {
            if (grouped.TryGetValue(d, out var agg))
                result.Add(new ServiceTrendPointDto(d, agg.Submitted, agg.Completed));
            else
                result.Add(new ServiceTrendPointDto(d, 0, 0));
        }

        logger.LogDebug("[GetServiceRequestTrends] Returning {Count} data points", result.Count);
        return result;
    }
}
