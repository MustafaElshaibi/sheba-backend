using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Sheba.Ministry.Domain.Entities;
using Sheba.Ministry.Domain.Enums;
using Sheba.Ministry.Infrastructure.Adapters;
using Sheba.Ministry.Infrastructure.Persistence;

namespace Sheba.Ministry.Tests.Infrastructure.Adapters;

public sealed class MinistryHealthAdapterTests
{
    private static MinistryDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<MinistryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MinistryDbContext(options);
    }

    [Fact]
    public async Task GetHealthSnapshotsAsync_NoMinistryFilter_ReturnsAllActiveConfigs()
    {
        await using var db = BuildContext();
        var ministryA = Guid.NewGuid();
        var ministryB = Guid.NewGuid();
        var configA = MinistryAuthConfig.Create(ministryA, "A", MinistryAuthType.ApiKey, "https://a.gov");
        var configB = MinistryAuthConfig.Create(ministryB, "B", MinistryAuthType.BasicAuth, "https://b.gov");
        configA.RecordHealthCheck(true, 12, null);
        db.AuthConfigs.AddRange(configA, configB);
        await db.SaveChangesAsync();

        var sut = new MinistryHealthAdapter(db, NullLogger<MinistryHealthAdapter>.Instance);
        var result = await sut.GetHealthSnapshotsAsync(null, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().Contain(s => s.AuthConfigId == configA.Id && s.IsHealthy == true && s.LatencyMs == 12);
        result.Should().Contain(s => s.AuthConfigId == configB.Id && s.IsHealthy == null);
    }

    [Fact]
    public async Task GetHealthSnapshotsAsync_WithMinistryFilter_NarrowsToThatMinistry()
    {
        await using var db = BuildContext();
        var ministryA = Guid.NewGuid();
        var ministryB = Guid.NewGuid();
        var configA = MinistryAuthConfig.Create(ministryA, "A", MinistryAuthType.ApiKey, "https://a.gov");
        var configB = MinistryAuthConfig.Create(ministryB, "B", MinistryAuthType.BasicAuth, "https://b.gov");
        db.AuthConfigs.AddRange(configA, configB);
        await db.SaveChangesAsync();

        var sut = new MinistryHealthAdapter(db, NullLogger<MinistryHealthAdapter>.Instance);
        var result = await sut.GetHealthSnapshotsAsync(ministryA, CancellationToken.None);

        result.Should().ContainSingle().Which.AuthConfigId.Should().Be(configA.Id);
    }

    [Fact]
    public async Task GetHealthSnapshotsAsync_InactiveConfig_Excluded()
    {
        await using var db = BuildContext();
        var config = MinistryAuthConfig.Create(Guid.NewGuid(), "Inactive", MinistryAuthType.ApiKey, "https://x.gov");
        config.Deactivate();
        db.AuthConfigs.Add(config);
        await db.SaveChangesAsync();

        var sut = new MinistryHealthAdapter(db, NullLogger<MinistryHealthAdapter>.Instance);
        var result = await sut.GetHealthSnapshotsAsync(null, CancellationToken.None);

        result.Should().BeEmpty();
    }
}
