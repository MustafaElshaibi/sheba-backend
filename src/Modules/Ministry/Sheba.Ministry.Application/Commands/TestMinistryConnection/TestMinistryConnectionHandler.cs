using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Ministry.Domain.Enums;
using Sheba.Ministry.Domain.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Ministry.Application.Commands.TestMinistryConnection;

/// <summary>
/// Tests connectivity to a ministry API using the configured auth adapter.
/// Selects the correct adapter by AuthType, decrypts credentials, calls health endpoint.
/// </summary>
public sealed class TestMinistryConnectionHandler(
    IMinistryRepository repository,
    IEnumerable<IMinistryAuthAdapter> adapters,
    ILogger<TestMinistryConnectionHandler> logger
) : IRequestHandler<TestMinistryConnectionCommand, TestMinistryConnectionResponse>
{
    public async Task<TestMinistryConnectionResponse> Handle(
        TestMinistryConnectionCommand request, CancellationToken ct)
    {
        var authConfig = await repository.GetAuthConfigByIdAsync(request.AuthConfigId, ct)
            ?? throw new NotFoundException("MinistryAuthConfig", request.AuthConfigId);

        var credential = authConfig.Credential
            ?? await repository.GetCredentialByAuthConfigIdAsync(request.AuthConfigId, ct)
            ?? throw new DomainException("No credentials configured for this auth config.");

        // Select adapter by auth type
        var adapterType = authConfig.AuthType.ToString();
        var adapter = adapters.FirstOrDefault(a =>
            string.Equals(a.AdapterType, adapterType, StringComparison.OrdinalIgnoreCase));

        if (adapter is null)
        {
            // For None auth type, just do a plain HTTP GET
            if (authConfig.AuthType == MinistryAuthType.None)
            {
                authConfig.RecordHealthCheck(true, 0, null);
                await repository.SaveChangesAsync(ct);
                return new TestMinistryConnectionResponse(true, null, 0, "No auth required — skipping test.");
            }
            throw new DomainException($"No adapter found for auth type '{adapterType}'.");
        }

        logger.LogInformation(
            "[TestConnection] Testing {AuthType} connection for AuthConfig {Id} ({Name})",
            adapterType, authConfig.Id, authConfig.Name);

        var result = await adapter.TestConnectionAsync(authConfig, credential, ct);

        credential.RecordVerification();
        // Persisted here so both this manual endpoint and the scheduled health sweep
        // (MinistryHealthSweepJob, which drives this same handler) update one status record.
        authConfig.RecordHealthCheck(result.Success, result.LatencyMs, result.Error);
        await repository.SaveChangesAsync(ct);

        logger.LogInformation(
            "[TestConnection] Result: Success={Success}, Status={Status}, Latency={Latency}ms",
            result.Success, result.StatusCode, result.LatencyMs);

        return new TestMinistryConnectionResponse(
            result.Success, result.StatusCode, result.LatencyMs, result.Error);
    }
}
