using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Admin.Application.Analytics.GetServiceRequestTrends;
using Sheba.Admin.Application.Interfaces;
using Sheba.Admin.Domain.Entities;

namespace Sheba.Admin.Tests.Application.Analytics;

public class GetServiceRequestTrendsHandlerTests
{
    private readonly IAdminAnalyticsRepository _analyticsRepo = Substitute.For<IAdminAnalyticsRepository>();
    private readonly GetServiceRequestTrendsHandler _handler;

    public GetServiceRequestTrendsHandlerTests()
    {
        _handler = new GetServiceRequestTrendsHandler(_analyticsRepo, NullLogger<GetServiceRequestTrendsHandler>.Instance);

        _analyticsRepo
            .GetServiceRequestSnapshotsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new List<DailyServiceRequestSnapshot>());
    }

    [Fact]
    public async Task Handle_WithMinistryId_ForwardsItToTheRepositoryQuery()
    {
        var ministryId = Guid.NewGuid();

        await _handler.Handle(new GetServiceRequestTrendsQuery(Days: 7, MinistryId: ministryId), CancellationToken.None);

        await _analyticsRepo.Received(1).GetServiceRequestSnapshotsAsync(
            Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), ministryId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoMinistryId_QueriesUnfiltered()
    {
        await _handler.Handle(new GetServiceRequestTrendsQuery(Days: 7, MinistryId: null), CancellationToken.None);

        await _analyticsRepo.Received(1).GetServiceRequestSnapshotsAsync(
            Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>());
    }
}
