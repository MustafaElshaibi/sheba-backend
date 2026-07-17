using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Sheba.Ministry.Domain.Entities;
using Sheba.Ministry.Domain.Interfaces;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Ministry.Infrastructure.Adapters;

/// <summary>
/// Implements <see cref="IMinistryCallPort"/>: loads the endpoint + auth config/credential,
/// authenticates via the matching <see cref="IMinistryAuthAdapter"/>, and sends the request —
/// all inside Ministry.Infrastructure so decrypted credential material never crosses the module
/// boundary (T-ARC-1). Registered in MinistryModule so ServiceRequest's workflow step handler can
/// invoke a ministry endpoint without referencing Ministry.Domain/Infrastructure directly.
/// </summary>
public sealed class MinistryCallPortAdapter(
    IMinistryRepository ministryRepo,
    IEnumerable<IMinistryAuthAdapter> authAdapters,
    IHttpClientFactory httpClientFactory,
    ILogger<MinistryCallPortAdapter> logger
) : IMinistryCallPort
{
    public async Task<MinistryCallResult> InvokeAsync(
        Guid endpointId, Guid citizenId, string? requestBodyJson, CancellationToken ct = default)
    {
        var endpoint = await ministryRepo.GetEndpointByIdAsync(endpointId, ct);
        if (endpoint is null)
            return new MinistryCallResult(false, null, 0, null, $"Ministry endpoint {endpointId} not found.");

        MinistryAuthConfig? authConfig = null;
        MinistryAuthCredential? credential = null;
        if (endpoint.AuthConfigId.HasValue)
        {
            authConfig = await ministryRepo.GetAuthConfigByIdAsync(endpoint.AuthConfigId.Value, ct);
            if (authConfig is not null)
                credential = authConfig.Credential ?? await ministryRepo.GetCredentialByAuthConfigIdAsync(authConfig.Id, ct);
        }

        var baseUrl = authConfig?.BaseUrl ?? "";
        var path = endpoint.PathTemplate.Replace(
            "{citizenId}", citizenId.ToString(), StringComparison.OrdinalIgnoreCase);

        var httpMethod = new HttpMethod(endpoint.HttpMethod);
        var httpRequest = new HttpRequestMessage(httpMethod, $"{baseUrl}{path}");

        if (httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put)
            httpRequest.Content = new StringContent(requestBodyJson ?? "{}", Encoding.UTF8, "application/json");

        if (authConfig is not null && credential is not null)
        {
            var adapter = authAdapters.FirstOrDefault(a =>
                string.Equals(a.AdapterType, authConfig.AuthType.ToString(), StringComparison.OrdinalIgnoreCase));
            if (adapter is not null)
                await adapter.AuthenticateRequestAsync(httpRequest, authConfig, credential, ct);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var client = httpClientFactory.CreateClient("MinistryClient");
            client.Timeout = TimeSpan.FromSeconds(endpoint.TimeoutSeconds);

            var response = await client.SendAsync(httpRequest, ct);
            sw.Stop();

            var responseBody = await response.Content.ReadAsStringAsync(ct);

            logger.LogInformation(
                "[MinistryCall] {Method} {Path} returned {Status} in {Ms}ms",
                endpoint.HttpMethod, path, (int)response.StatusCode, sw.ElapsedMilliseconds);

            return new MinistryCallResult(
                response.IsSuccessStatusCode, (int)response.StatusCode, sw.ElapsedMilliseconds, responseBody,
                response.IsSuccessStatusCode ? null : $"Ministry API returned {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "[MinistryCall] Exception calling {Path}", path);
            return new MinistryCallResult(false, null, sw.ElapsedMilliseconds, null, ex.Message);
        }
    }
}
