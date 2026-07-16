using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Sheba.Ministry.Domain.Entities;
using Sheba.Ministry.Domain.Interfaces;

namespace Sheba.Ministry.Infrastructure.Adapters;

/// <summary>
/// Authenticates requests using HTTP Basic auth (username:password base64).
/// </summary>
public sealed class BasicAuthMinistryAuthAdapter(
    ICredentialEncryptor encryptor,
    IHttpClientFactory httpClientFactory,
    ILogger<BasicAuthMinistryAuthAdapter> logger) : IMinistryAuthAdapter
{
    public string AdapterType => "BasicAuth";

    public Task AuthenticateRequestAsync(
        HttpRequestMessage request,
        MinistryAuthConfig authConfig,
        MinistryAuthCredential credential,
        CancellationToken ct = default)
    {
        var username = encryptor.Decrypt(credential.BasicUsername ?? "");
        var password = encryptor.Decrypt(credential.BasicPassword ?? "");
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
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
            logger.LogError(ex, "[BasicAuthAdapter] Connection test failed for {BaseUrl}", authConfig.BaseUrl);
            return new MinistryConnectionTestResult(false, null, sw.ElapsedMilliseconds, ex.Message);
        }
    }
}
