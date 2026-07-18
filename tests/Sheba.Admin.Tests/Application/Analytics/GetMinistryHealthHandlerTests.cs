using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Admin.Application.Analytics.GetMinistryHealth;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Admin.Tests.Application.Analytics;

public class GetMinistryHealthHandlerTests
{
    private readonly IMinistryHealthProvider _ministryHealth = Substitute.For<IMinistryHealthProvider>();
    private readonly GetMinistryHealthHandler _handler;

    public GetMinistryHealthHandlerTests()
    {
        _handler = new GetMinistryHealthHandler(_ministryHealth, NullLogger<GetMinistryHealthHandler>.Instance);
    }

    [Fact]
    public async Task Handle_NoMinistryId_PassesNullThrough()
    {
        await _handler.Handle(new GetMinistryHealthQuery(MinistryId: null), CancellationToken.None);

        await _ministryHealth.Received(1).GetHealthSnapshotsAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithMinistryId_NarrowsToThatMinistry()
    {
        var ministryId = Guid.NewGuid();

        await _handler.Handle(new GetMinistryHealthQuery(MinistryId: ministryId), CancellationToken.None);

        await _ministryHealth.Received(1).GetHealthSnapshotsAsync(ministryId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsProviderSnapshotsUnchanged()
    {
        var snapshots = new List<MinistryHealthSnapshot>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Primary", "ApiKey", DateTime.UtcNow, true, 42, null)
        };
        _ministryHealth.GetHealthSnapshotsAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(snapshots);

        var result = await _handler.Handle(new GetMinistryHealthQuery(), CancellationToken.None);

        result.Should().BeEquivalentTo(snapshots);
    }
}
