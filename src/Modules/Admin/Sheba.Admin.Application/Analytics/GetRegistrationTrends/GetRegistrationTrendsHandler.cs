using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Admin.Application.Interfaces;

namespace Sheba.Admin.Application.Analytics.GetRegistrationTrends;

/// <summary>
/// Reads daily registration snapshots for the last N days.
/// Missing days are filled with zeros so the chart has a continuous x-axis.
/// </summary>
public sealed class GetRegistrationTrendsHandler(
    IAdminAnalyticsRepository analyticsRepo,
    ILogger<GetRegistrationTrendsHandler> logger
) : IRequestHandler<GetRegistrationTrendsQuery, List<TrendPointDto>>
{
    public async Task<List<TrendPointDto>> Handle(GetRegistrationTrendsQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = today.AddDays(-(request.Days - 1));

        var snapshots = await analyticsRepo.GetRegistrationSnapshotsAsync(from, today, ct);
        var lookup = snapshots.ToDictionary(s => s.Date);

        // Fill missing days with zeros for continuous chart data
        var result = new List<TrendPointDto>(request.Days);
        for (var d = from; d <= today; d = d.AddDays(1))
        {
            if (lookup.TryGetValue(d, out var snap))
            {
                result.Add(new TrendPointDto(d, snap.TotalRegistrations, snap.Approved, snap.Rejected));
            }
            else
            {
                result.Add(new TrendPointDto(d, 0, 0, 0));
            }
        }

        logger.LogDebug("[GetRegistrationTrends] Returning {Count} data points", result.Count);
        return result;
    }
}
