using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Sheba.Ministry.Domain.Entities;
using Sheba.Ministry.Domain.Interfaces;

namespace Sheba.Ministry.Infrastructure.Adapters;

/// <summary>
/// Authenticates requests using a static bearer token in the Authorization header.
/// </summary>
public sealed class BearerTokenMinistryAuthAdapter(
    ICredentialEncryptor encryptor,
    IHttpClientFactory httpClientFactory,
    ILogger<BearerTokenMinistryAuthAdapter> logger) : IMinistryAuthAdapter
{
    public string AdapterType => "BearerToken";

    public Task AuthenticateRequestAsync(
        HttpRequestMessage request,
        MinistryAuthConfig authConfig,
        MinistryAuthCredential credential,
        CancellationToken ct = default)
    {
        var decryptedToken = encryptor.Decrypt(credential.BearerToken ?? "");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", decryptedToken);
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
            logger.LogError(ex, "[BearerTokenAdapter] Connection test failed for {BaseUrl}", authConfig.BaseUrl);
            return new MinistryConnectionTestResult(false, null, sw.ElapsedMilliseconds, ex.Message);
        }
    }
}
