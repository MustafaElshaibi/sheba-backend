using Sheba.Ministry.Domain.Enums;
using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Ministry.Domain.Entities;

/// <summary>
/// Auth configuration for connecting TO the ministry's external system.
/// Each ministry system may require a different auth method.
/// </summary>
public sealed class MinistryAuthConfig : BaseEntity
{
    public Guid MinistryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public MinistryAuthType AuthType { get; private set; }
    public string BaseUrl { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public bool IsDefault { get; private set; }
    public string? HealthCheckPath { get; private set; }
    public int TimeoutSeconds { get; private set; } = 30;
    public int RetryCount { get; private set; } = 3;

    // Health dashboard (ministry health sweep — Phase 2 roadmap item)
    public DateTime? LastHealthCheckAt { get; private set; }
    public bool? LastHealthSuccess { get; private set; }
    public long? LastHealthLatencyMs { get; private set; }
    public string? LastHealthError { get; private set; }

    // Navigation
    public MinistryAuthCredential? Credential { get; private set; }

    // EF Core
    private MinistryAuthConfig() { }

    public static MinistryAuthConfig Create(
        Guid ministryId,
        string name,
        MinistryAuthType authType,
        string baseUrl,
        bool isDefault = false,
        string? healthCheckPath = null,
        int timeoutSeconds = 30,
        int retryCount = 3)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Auth config name is required.");
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new DomainException("Base URL is required.");

        return new MinistryAuthConfig
        {
            MinistryId = ministryId,
            Name = name.Trim(),
            AuthType = authType,
            BaseUrl = baseUrl.TrimEnd('/'),
            IsDefault = isDefault,
            HealthCheckPath = healthCheckPath,
            TimeoutSeconds = timeoutSeconds,
            RetryCount = retryCount
        };
    }

    public void Update(string name, string baseUrl, string? healthCheckPath, int timeoutSeconds, int retryCount)
    {
        Name = name.Trim();
        BaseUrl = baseUrl.TrimEnd('/');
        HealthCheckPath = healthCheckPath;
        TimeoutSeconds = timeoutSeconds;
        RetryCount = retryCount;
        Touch();
    }

    public void SetDefault(bool isDefault) { IsDefault = isDefault; Touch(); }
    public void Activate() { IsActive = true; Touch(); }
    public void Deactivate() { IsActive = false; Touch(); }

    public void SetCredential(MinistryAuthCredential credential)
    {
        Credential = credential;
        Touch();
    }

    /// <summary>
    /// Records the outcome of an IMinistryAuthAdapter.TestConnectionAsync call — used both by the
    /// manual "test connection" endpoint and the scheduled health sweep. Deliberately does not
    /// call Touch(): health status is operational telemetry, not a configuration edit, so it
    /// should not bump UpdatedAt / show up as a config change.
    /// </summary>
    public void RecordHealthCheck(bool success, long latencyMs, string? error)
    {
        LastHealthCheckAt = DateTime.UtcNow;
        LastHealthSuccess = success;
        LastHealthLatencyMs = latencyMs;
        LastHealthError = error;
    }
}
