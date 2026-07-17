using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Admin.Application.Analytics.GetKpiSummary;
using Sheba.Admin.Application.Interfaces;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Admin.Tests.Application.Analytics;

public class GetKpiSummaryHandlerTests
{
    private readonly IIdentityStatsProvider _identityStats = Substitute.For<IIdentityStatsProvider>();
    private readonly IAdminAnalyticsRepository _analyticsRepo = Substitute.For<IAdminAnalyticsRepository>();
    private readonly GetKpiSummaryHandler _handler;

    public GetKpiSummaryHandlerTests()
    {
        _handler = new GetKpiSummaryHandler(_identityStats, _analyticsRepo, NullLogger<GetKpiSummaryHandler>.Instance);
    }

    [Fact]
    public async Task Handle_NoMinistryId_PassesNullThroughToMinistrySliceableQueries()
    {
        await _handler.Handle(new GetKpiSummaryQuery(MinistryId: null), CancellationToken.None);

        await _analyticsRepo.Received(1).GetTodayCompletionsAsync(null, Arg.Any<CancellationToken>());
        await _analyticsRepo.Received(1).GetSlaBreachCountLast30DaysAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithMinistryId_NarrowsServiceRequestBasedQueriesToThatMinistry()
    {
        var ministryId = Guid.NewGuid();

        await _handler.Handle(new GetKpiSummaryQuery(MinistryId: ministryId), CancellationToken.None);

        await _analyticsRepo.Received(1).GetTodayCompletionsAsync(ministryId, Arg.Any<CancellationToken>());
        await _analyticsRepo.Received(1).GetSlaBreachCountLast30DaysAsync(ministryId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithMinistryId_LeavesRegistrationBasedFiguresGlobal()
    {
        // Registrations aren't ministry-owned entities — TotalAccounts, PendingIdentityRequests
        // and AvgApprovalHoursLast30Days must stay unfiltered even when scoped to a ministry.
        _identityStats.GetTotalAccountsAsync(Arg.Any<CancellationToken>()).Returns(42);
        _identityStats.GetPendingIdentityRequestsAsync(Arg.Any<CancellationToken>()).Returns(7);
        _analyticsRepo.GetAvgApprovalHoursLast30DaysAsync(Arg.Any<CancellationToken>()).Returns(3.5m);

        var result = await _handler.Handle(new GetKpiSummaryQuery(MinistryId: Guid.NewGuid()), CancellationToken.None);

        result.TotalAccounts.Should().Be(42);
        result.PendingIdentityRequests.Should().Be(7);
        result.AvgApprovalHoursLast30Days.Should().Be(3.5m);
        await _analyticsRepo.Received(1).GetAvgApprovalHoursLast30DaysAsync(Arg.Any<CancellationToken>());
    }
}
