using FluentAssertions;
using Sheba.Ministry.Domain.Entities;
using Sheba.Ministry.Domain.Enums;

namespace Sheba.Ministry.Tests.Domain;

public sealed class MinistryAuthConfigTests
{
    private static MinistryAuthConfig BuildConfig() =>
        MinistryAuthConfig.Create(
            ministryId: Guid.NewGuid(),
            name: "Primary",
            authType: MinistryAuthType.ApiKey,
            baseUrl: "https://ministry.example.gov");

    [Fact]
    public void RecordHealthCheck_Success_StoresLatestSnapshot()
    {
        var config = BuildConfig();

        config.RecordHealthCheck(success: true, latencyMs: 42, error: null);

        config.LastHealthSuccess.Should().BeTrue();
        config.LastHealthLatencyMs.Should().Be(42);
        config.LastHealthError.Should().BeNull();
        config.LastHealthCheckAt.Should().NotBeNull();
    }

    [Fact]
    public void RecordHealthCheck_Failure_StoresErrorMessage()
    {
        var config = BuildConfig();

        config.RecordHealthCheck(success: false, latencyMs: 5000, error: "timeout");

        config.LastHealthSuccess.Should().BeFalse();
        config.LastHealthLatencyMs.Should().Be(5000);
        config.LastHealthError.Should().Be("timeout");
    }

    [Fact]
    public void RecordHealthCheck_DoesNotChangeUpdatedAt()
    {
        var config = BuildConfig();
        var updatedAtBefore = config.UpdatedAt;

        config.RecordHealthCheck(success: true, latencyMs: 10, error: null);

        // Health status is operational telemetry, not a config edit — must not bump UpdatedAt.
        config.UpdatedAt.Should().Be(updatedAtBefore);
    }

    [Fact]
    public void RecordHealthCheck_OverwritesPreviousSnapshot()
    {
        var config = BuildConfig();
        config.RecordHealthCheck(success: true, latencyMs: 10, error: null);

        config.RecordHealthCheck(success: false, latencyMs: 999, error: "connection reset");

        config.LastHealthSuccess.Should().BeFalse();
        config.LastHealthLatencyMs.Should().Be(999);
        config.LastHealthError.Should().Be("connection reset");
    }
}
