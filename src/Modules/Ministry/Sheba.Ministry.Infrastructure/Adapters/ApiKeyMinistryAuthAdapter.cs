using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Sheba.Ministry.Domain.Entities;
using Sheba.Ministry.Domain.Interfaces;

namespace Sheba.Ministry.Infrastructure.Adapters;

/// <summary>
/// Authenticates requests using a static API key placed in a header, query param, or cookie.
/// </summary>
public sealed class ApiKeyMinistryAuthAdapter(
    ICredentialEncryptor encryptor,
    IHttpClientFactory httpClientFactory,
    ILogger<ApiKeyMinistryAuthAdapter> logger) : IMinistryAuthAdapter
{
    public string AdapterType => "ApiKey";

    public Task AuthenticateRequestAsync(
        HttpRequestMessage request,
        MinistryAuthConfig authConfig,
        MinistryAuthCredential credential,
        CancellationToken ct = default)
    {
        var headerName = credential.ApiKeyHeaderName ?? "X-Api-Key";
        var decryptedKey = encryptor.Decrypt(credential.ApiKeyValue ?? "");

        switch (credential.ApiKeyPlacementType)
        {
            case Domain.Enums.ApiKeyPlacement.Query:
                var uri = request.RequestUri!;
                var separator = uri.Query.Contains('?') ? "&" : "?";
                request.RequestUri = new Uri($"{uri}{separator}{headerName}={Uri.EscapeDataString(decryptedKey)}");
                break;
            case Domain.Enums.ApiKeyPlacement.Cookie:
                request.Headers.Add("Cookie", $"{headerName}={decryptedKey}");
                break;
            default: // Header
                request.Headers.Add(headerName, decryptedKey);
                break;
        }

        return Task.CompletedTask;
    }

    public async Task<MinistryConnectionTestResult> TestConnectionAsync(
        MinistryAuthConfig authConfig,
        MinistryAuthCredential credential,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var client = httpClientFactory.CreateClient("MinistryClient");
            client.Timeout = TimeSpan.FromSeconds(authConfig.TimeoutSeconds);

            var path = authConfig.HealthCheckPath ?? "/health";
            var request = new HttpRequestMessage(HttpMethod.Get, $"{authConfig.BaseUrl}{path}");
            await AuthenticateRequestAsync(request, authConfig, credential, ct);

            var response = await client.SendAsync(request, ct);
            sw.Stop();

            return new MinistryConnectionTestResult(
                response.IsSuccessStatusCode, (int)response.StatusCode, sw.ElapsedMilliseconds, null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "[ApiKeyAdapter] Connection test failed for {BaseUrl}", authConfig.BaseUrl);
            return new MinistryConnectionTestResult(false, null, sw.ElapsedMilliseconds, ex.Message);
        }
    }
}
