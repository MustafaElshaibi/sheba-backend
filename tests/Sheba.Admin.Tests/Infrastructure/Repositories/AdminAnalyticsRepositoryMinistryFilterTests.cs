using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Sheba.Admin.Domain.Entities;
using Sheba.Admin.Infrastructure.Persistence;
using Sheba.Admin.Infrastructure.Repositories;

namespace Sheba.Admin.Tests.Infrastructure.Repositories;

/// <summary>
/// T-AUTH-3: verifies the ministryId filter on the service-request-based aggregated queries
/// actually narrows results, using a real (in-memory) EF query rather than a mocked repository.
/// </summary>
public class AdminAnalyticsRepositoryMinistryFilterTests
{
    private static AdminDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AdminDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AdminDbContext(options);
    }

    [Fact]
    public async Task GetTodayCompletionsAsync_WithMinistryId_CountsOnlyThatMinistry()
    {
        await using var db = CreateContext();
        var ministryA = Guid.NewGuid();
        var ministryB = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var snapshotA = DailyServiceRequestSnapshot.Create(today, Guid.NewGuid(), ministryA);
        snapshotA.IncrementCompleted();
        snapshotA.IncrementCompleted();
        var snapshotB = DailyServiceRequestSnapshot.Create(today, Guid.NewGuid(), ministryB);
        snapshotB.IncrementCompleted();

        db.DailyServiceRequestSnapshots.AddRange(snapshotA, snapshotB);
        await db.SaveChangesAsync();

        var repo = new AdminAnalyticsRepository(db);

        (await repo.GetTodayCompletionsAsync(ministryA, CancellationToken.None)).Should().Be(2);
        (await repo.GetTodayCompletionsAsync(ministryB, CancellationToken.None)).Should().Be(1);
        (await repo.GetTodayCompletionsAsync(null, CancellationToken.None)).Should().Be(3);
    }

    [Fact]
    public async Task GetSlaBreachCountLast30DaysAsync_WithMinistryId_CountsOnlyThatMinistry()
    {
        await using var db = CreateContext();
        var ministryA = Guid.NewGuid();
        var ministryB = Guid.NewGuid();
        var recent = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        var snapshotA = DailyServiceRequestSnapshot.Create(recent, Guid.NewGuid(), ministryA);
        snapshotA.IncrementSlaBreach();
        var snapshotB = DailyServiceRequestSnapshot.Create(recent, Guid.NewGuid(), ministryB);
        snapshotB.IncrementSlaBreach();
        snapshotB.IncrementSlaBreach();

        db.DailyServiceRequestSnapshots.AddRange(snapshotA, snapshotB);
        await db.SaveChangesAsync();

        var repo = new AdminAnalyticsRepository(db);

        (await repo.GetSlaBreachCountLast30DaysAsync(ministryA, CancellationToken.None)).Should().Be(1);
        (await repo.GetSlaBreachCountLast30DaysAsync(ministryB, CancellationToken.None)).Should().Be(2);
        (await repo.GetSlaBreachCountLast30DaysAsync(null, CancellationToken.None)).Should().Be(3);
    }

    [Fact]
    public async Task GetServiceRequestSnapshotsAsync_WithMinistryId_ReturnsOnlyThatMinistrysSnapshots()
    {
        await using var db = CreateContext();
        var ministryA = Guid.NewGuid();
        var ministryB = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        db.DailyServiceRequestSnapshots.AddRange(
            DailyServiceRequestSnapshot.Create(today, Guid.NewGuid(), ministryA),
            DailyServiceRequestSnapshot.Create(today, Guid.NewGuid(), ministryB));
        await db.SaveChangesAsync();

        var repo = new AdminAnalyticsRepository(db);

        var scoped = await repo.GetServiceRequestSnapshotsAsync(today, today, ministryA, CancellationToken.None);
        scoped.Should().ContainSingle().Which.MinistryId.Should().Be(ministryA);

        var unscoped = await repo.GetServiceRequestSnapshotsAsync(today, today, null, CancellationToken.None);
        unscoped.Should().HaveCount(2);
    }
}
