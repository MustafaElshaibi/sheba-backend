using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sheba.Identity.Domain.Interfaces;

namespace Sheba.Identity.Infrastructure.Adapters;

/// <summary>Bound from the <c>NationalId:OpenCrvs</c> config section.</summary>
public sealed class OpenCrvsOptions
{
    public const string SectionName = "NationalId:OpenCrvs";

    public string GraphQlEndpoint { get; set; } = "";
    public string TokenEndpoint { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// OpenCRVS civil-registry adapter (T-INT-1) — the second concrete <see cref="INationalIdProvider"/>
/// shape referenced by sheba.md §6.5 and the roadmap's "real civil-registry API differs wildly from
/// the assumed contract" risk row. Deliberately a different transport and auth shape than
/// <see cref="HttpNationalIdProvider"/>: OAuth2 client_credentials (cached bearer token, not
/// per-request creds) against a GraphQL endpoint rather than plain REST — proving the
/// <see cref="INationalIdProvider"/> port tolerates both.
///
/// OpenCRVS itself models birth/death *registration events*, not a standalone "is this NID valid"
/// directory, so a production deployment fronts it with a gateway/resolver that projects
/// registration status onto the identity-check shape this adapter's query expects (nationalId,
/// phoneNumber, name, dateOfBirth, gender, status). That gateway contract — not OpenCRVS's raw
/// public schema — is what <see cref="OpenCrvsOptions.GraphQlEndpoint"/> points at.
/// </summary>
public sealed class OpenCrvsNationalIdProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<OpenCrvsOptions> options,
    IMemoryCache tokenCache,
    ILogger<OpenCrvsNationalIdProvider> logger) : INationalIdProvider
{
    private const string TokenCacheKey = "OpenCrvs:AccessToken";

    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web);

    private const string CitizenIdentityQuery = """
        query CitizenIdentity($nationalId: String!) {
          citizenIdentity(nationalId: $nationalId) {
            nationalId
            phoneNumber
            fullNameAr
            fullNameEn
            dateOfBirth
            gender
            status
          }
        }
        """;

    public async Task<NationalIdLookupResult> LookupAsync(
        string nationalId,
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var client = httpClientFactory.CreateClient("OpenCrvs");
        client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);

        logger.LogInformation("[OpenCrvsNID] Looking up NID {NationalId}", MaskNid(nationalId));

        var token = await GetOrRefreshTokenAsync(client, cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, opts.GraphQlEndpoint)
        {
            Content = JsonContent.Create(
                new OpenCrvsGraphQlRequest(CitizenIdentityQuery, new OpenCrvsQueryVariables(nationalId)),
                options: WireOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Registry outage: fail closed (sheba.md §6.5) — never let an unreachable registry
            // silently read as NotFound, or an onboarding outage becomes indistinguishable from
            // a citizen who genuinely doesn't exist.
            logger.LogError(ex, "[OpenCrvsNID] Registry unreachable for NID {NationalId}", MaskNid(nationalId));
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "[OpenCrvsNID] Registry returned {StatusCode} for NID {NationalId}: {Body}",
                (int)response.StatusCode, MaskNid(nationalId), body);
            throw new HttpRequestException($"OpenCRVS registry returned {(int)response.StatusCode}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<OpenCrvsGraphQlResponse>(
            WireOptions, cancellationToken);

        if (payload?.Errors is { Count: > 0 } errors)
        {
            var message = string.Join("; ", errors.Select(e => e.Message));
            logger.LogError(
                "[OpenCrvsNID] GraphQL error for NID {NationalId}: {Message}", MaskNid(nationalId), message);
            throw new HttpRequestException($"OpenCRVS GraphQL error: {message}");
        }

        var record = payload?.Data?.CitizenIdentity;
        if (record is null)
        {
            logger.LogWarning("[OpenCrvsNID] NID {NationalId} not found in registry", MaskNid(nationalId));
            return NotFound(nationalId);
        }

        var registeredPhone = NormalizePhone(record.PhoneNumber ?? "");
        if (!string.Equals(registeredPhone, NormalizePhone(phoneNumber), StringComparison.OrdinalIgnoreCase))
        {
            // No info leakage — indistinguishable from a true not-found, mirrors MockNationalIdProvider.
            logger.LogWarning("[OpenCrvsNID] NID {NationalId} phone mismatch", MaskNid(nationalId));
            return NotFound(nationalId);
        }

        var status = MapStatus(record.Status);

        logger.LogInformation(
            "[OpenCrvsNID] NID {NationalId} found: Status={Status}", MaskNid(nationalId), status);

        return new NationalIdLookupResult(
            IsFound: true,
            NationalId: nationalId,
            FullNameAr: record.FullNameAr ?? "",
            FullNameEn: record.FullNameEn ?? "",
            PhoneNumber: record.PhoneNumber ?? "",
            DateOfBirth: DateOnly.TryParse(record.DateOfBirth, out var dob) ? dob : default,
            Gender: record.Gender ?? "",
            Status: status);
    }

    private static NationalIdLookupResult NotFound(string nationalId) => new(
        IsFound: false,
        NationalId: nationalId,
        FullNameAr: "",
        FullNameEn: "",
        PhoneNumber: "",
        DateOfBirth: default,
        Gender: "",
        Status: NidStatus.NotFound);

    private static NidStatus MapStatus(string? status) => status?.ToUpperInvariant() switch
    {
        "ACTIVE"    => NidStatus.Valid,
        "DECEASED"  => NidStatus.Deceased,
        "SUSPENDED" => NidStatus.Suspended,
        "EXPIRED"   => NidStatus.Expired,
        _           => NidStatus.NotFound,
    };

    private async Task<string> GetOrRefreshTokenAsync(HttpClient client, CancellationToken ct)
    {
        if (tokenCache.TryGetValue<string>(TokenCacheKey, out var cached) && cached is not null)
            return cached;

        var opts = options.Value;
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, opts.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = opts.ClientId,
                ["client_secret"] = opts.ClientSecret,
            })
        };

        var response = await client.SendAsync(tokenRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError(
                "[OpenCrvsNID] Token request failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
            throw new HttpRequestException($"OpenCRVS token endpoint returned {(int)response.StatusCode}.");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var accessToken = doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new HttpRequestException("OpenCRVS token endpoint returned no access_token.");
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;

        // 60s buffer, mirroring OidcMinistryAuthAdapter's token-refresh margin.
        tokenCache.Set(TokenCacheKey, accessToken, TimeSpan.FromSeconds(Math.Max(1, expiresIn - 60)));

        return accessToken;
    }

    private static string NormalizePhone(string phone)
    {
        var trimmed = phone.Trim().Replace(" ", "").Replace("-", "");
        if (trimmed.StartsWith("+967")) return trimmed;
        if (trimmed.StartsWith("967")) return "+" + trimmed;
        if (trimmed.StartsWith("0")) return "+967" + trimmed[1..];
        return "+967" + trimmed;
    }

    private static string MaskNid(string nid) =>
        nid.Length > 4 ? string.Concat(new string('*', nid.Length - 4), nid[^4..]) : "****";
}

internal sealed record OpenCrvsGraphQlRequest(string Query, OpenCrvsQueryVariables Variables);

internal sealed record OpenCrvsQueryVariables(string NationalId);

internal sealed record OpenCrvsGraphQlResponse(
    OpenCrvsData? Data,
    IReadOnlyList<OpenCrvsGraphQlError>? Errors);

internal sealed record OpenCrvsData(OpenCrvsCitizenRecord? CitizenIdentity);

internal sealed record OpenCrvsCitizenRecord(
    string? NationalId,
    string? PhoneNumber,
    string? FullNameAr,
    string? FullNameEn,
    string? DateOfBirth,
    string? Gender,
    string? Status);

internal sealed record OpenCrvsGraphQlError(string Message);
