using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sheba.Ministry.Domain.Entities;
using Sheba.Ministry.Domain.Interfaces;

namespace Sheba.Ministry.Infrastructure.Adapters;

/// <summary>
/// Authenticates requests using OIDC/OAuth2 client_credentials flow.
/// Manages a token cache: if the cached token is still valid, reuse it;
/// otherwise, request a fresh token from the ministry's token endpoint.
/// </summary>
public sealed class OidcMinistryAuthAdapter(
    ICredentialEncryptor encryptor,
    IHttpClientFactory httpClientFactory,
    ILogger<OidcMinistryAuthAdapter> logger) : IMinistryAuthAdapter
{
    public string AdapterType => "Oidc";

    public async Task AuthenticateRequestAsync(
        HttpRequestMessage request,
        MinistryAuthConfig authConfig,
        MinistryAuthCredential credential,
        CancellationToken ct = default)
    {
        var token = await GetOrRefreshTokenAsync(authConfig, credential, ct);
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<MinistryConnectionTestResult> TestConnectionAsync(
        MinistryAuthConfig authConfig,
        MinistryAuthCredential credential,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // Step 1: Try to obtain a token
            var token = await GetOrRefreshTokenAsync(authConfig, credential, ct);
            if (string.IsNullOrEmpty(token))
            {
                sw.Stop();
                return new MinistryConnectionTestResult(false, null, sw.ElapsedMilliseconds,
                    "Failed to obtain access token from OIDC token endpoint.");
            }

            // Step 2: Call health endpoint with the token
            var client = httpClientFactory.CreateClient("MinistryClient");
            client.Timeout = TimeSpan.FromSeconds(authConfig.TimeoutSeconds);

            var path = authConfig.HealthCheckPath ?? "/health";
            var request = new HttpRequestMessage(HttpMethod.Get, $"{authConfig.BaseUrl}{path}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.SendAsync(request, ct);
            sw.Stop();

            return new MinistryConnectionTestResult(
                response.IsSuccessStatusCode, (int)response.StatusCode, sw.ElapsedMilliseconds, null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "[OidcAdapter] Connection test failed for {BaseUrl}", authConfig.BaseUrl);
            return new MinistryConnectionTestResult(false, null, sw.ElapsedMilliseconds, ex.Message);
        }
    }

    private async Task<string?> GetOrRefreshTokenAsync(
        MinistryAuthConfig authConfig,
        MinistryAuthCredential credential,
        CancellationToken ct)
    {
        // Check if cached token is still valid (with 60s buffer)
        if (!string.IsNullOrEmpty(credential.CachedAccessToken)
            && credential.TokenExpiresAt.HasValue
            && credential.TokenExpiresAt.Value > DateTime.UtcNow.AddSeconds(60))
        {
            return encryptor.Decrypt(credential.CachedAccessToken);
        }

        // Request fresh token via client_credentials
        var tokenEndpoint = credential.OidcTokenEndpoint;
        if (string.IsNullOrEmpty(tokenEndpoint))
        {
            logger.LogWarning("[OidcAdapter] No token endpoint configured for AuthConfig {Id}", authConfig.Id);
            return null;
        }

        var clientId = encryptor.Decrypt(credential.OidcClientId ?? "");
        var clientSecret = encryptor.Decrypt(credential.OidcClientSecret ?? "");
        var scope = credential.OidcScope ?? "api";

        try
        {
            var client = httpClientFactory.CreateClient("MinistryClient");
            var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
            tokenRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["scope"] = scope
            });

            var tokenResponse = await client.SendAsync(tokenRequest, ct);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                var body = await tokenResponse.Content.ReadAsStringAsync(ct);
                logger.LogError("[OidcAdapter] Token request failed ({Status}): {Body}",
                    tokenResponse.StatusCode, body);
                return null;
            }

            var json = await tokenResponse.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var accessToken = doc.RootElement.GetProperty("access_token").GetString();
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;

            // Cache the token (encrypted)
            credential.UpdateCachedToken(
                encryptor.Encrypt(accessToken ?? ""),
                DateTime.UtcNow.AddSeconds(expiresIn));

            logger.LogInformation("[OidcAdapter] Refreshed token for AuthConfig {Id}, expires in {Seconds}s",
                authConfig.Id, expiresIn);

            return accessToken;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[OidcAdapter] Failed to refresh token for AuthConfig {Id}", authConfig.Id);
            return null;
        }
    }
}
