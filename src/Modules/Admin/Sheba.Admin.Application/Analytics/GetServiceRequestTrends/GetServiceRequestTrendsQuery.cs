using MediatR;

namespace Sheba.Admin.Application.Analytics.GetServiceRequestTrends;

/// <summary>
/// Returns daily service request counts for the last N days — suitable for charts.
/// </summary>
/// <param name="MinistryId">T-AUTH-3: narrows to one ministry (a MinistryManager's claim);
/// null for SuperAdmin/Auditor aggregates across all ministries.</param>
public sealed record GetServiceRequestTrendsQuery(int Days = 30, Guid? MinistryId = null) : IRequest<List<ServiceTrendPointDto>>;

/// <summary>
/// A single data point for service request trend charts.
/// </summary>
public sealed record ServiceTrendPointDto(
    DateOnly Date,
    int Submitted,
    int Completed
);
