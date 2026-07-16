using MediatR;

namespace Sheba.Admin.Application.Analytics.GetServiceRequestTrends;

/// <summary>
/// Returns daily service request counts for the last N days — suitable for charts.
/// </summary>
public sealed record GetServiceRequestTrendsQuery(int Days = 30) : IRequest<List<ServiceTrendPointDto>>;

/// <summary>
/// A single data point for service request trend charts.
/// </summary>
public sealed record ServiceTrendPointDto(
    DateOnly Date,
    int Submitted,
    int Completed
);
