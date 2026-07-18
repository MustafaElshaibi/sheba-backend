namespace Sheba.Shared.Kernel.Interfaces;

/// <summary>
/// Cross-module port ServiceRequest uses to invoke a ministry API endpoint during a workflow's
/// MinistryApiCall step. Defined in Shared.Kernel so ServiceRequest never references
/// Sheba.Ministry.Domain/Infrastructure directly (rule 1/3, T-ARC-1) — and, more importantly, so
/// decrypted ministry credential material (<c>MinistryAuthCredential</c>) never leaves the
/// Ministry module: this port builds, authenticates, and sends the request entirely inside
/// Ministry.Infrastructure, returning only the outcome.
/// </summary>
public interface IMinistryCallPort
{
    /// <summary>
    /// Loads the endpoint + auth config/credential, substitutes <c>{citizenId}</c> in the path
    /// template, authenticates, and sends the request. <paramref name="requestBodyJson"/> is sent
    /// as the body for POST/PUT endpoints.
    /// </summary>
    Task<MinistryCallResult> InvokeAsync(
        Guid endpointId, Guid citizenId, string? requestBodyJson, CancellationToken ct = default);
}

public sealed record MinistryCallResult(
    bool Success,
    int? StatusCode,
    long DurationMs,
    string? ResponseBody,
    string? ErrorMessage,
    bool RateLimited = false); // true = the endpoint's RateLimitPerMinute was exceeded; call never sent (T-MIN-1)
