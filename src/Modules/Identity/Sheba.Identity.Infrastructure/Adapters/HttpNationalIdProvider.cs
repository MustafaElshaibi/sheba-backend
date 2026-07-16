using Microsoft.Extensions.Logging;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Domain.Interfaces;

namespace Sheba.Identity.Infrastructure.Adapters;

/// <summary>
/// Production NID adapter — calls the real civil registry HTTP API.
/// Active when: NationalId:ActiveProvider = "Http"
/// 
/// TODO (Week 2–3): Implement the actual HTTP call once civil registry API details are confirmed.
/// For graduation: the Mock provider is used, this is registered but never called.
/// </summary>
#pragma warning disable CS9113 // parameter unread — intentional stub
public sealed class HttpNationalIdProvider(
    IHttpClientFactory _,
    ILogger<HttpNationalIdProvider> logger) : INationalIdProvider
{
#pragma warning restore CS9113
    public Task<NationalIdLookupResult> LookupAsync(
        string nationalId,
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[HttpNID] Looking up NID {NationalId}", nationalId);

        // TODO: Replace with real civil registry endpoint
        // var client = httpClientFactory.CreateClient("CivilRegistry");
        // var response = await client.GetAsync($"/api/citizens/{nationalId}", cancellationToken);

        throw new NotImplementedException(
            "HttpNationalIdProvider is not yet implemented. " +
            "Set NationalId:ActiveProvider=Mock in appsettings to use the mock provider.");
    }
}
