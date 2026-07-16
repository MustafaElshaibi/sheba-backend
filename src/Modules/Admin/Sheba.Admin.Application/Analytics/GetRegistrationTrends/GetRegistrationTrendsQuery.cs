using MediatR;

namespace Sheba.Admin.Application.Analytics.GetRegistrationTrends;

/// <summary>
/// Returns daily registration counts for the last N days — suitable for charts.
/// </summary>
public sealed record GetRegistrationTrendsQuery(int Days = 30) : IRequest<List<TrendPointDto>>;

/// <summary>
/// A single data point in a time-series chart.
/// </summary>
public sealed record TrendPointDto(
    DateOnly Date,
    int TotalRegistrations,
    int Approved,
    int Rejected
);
