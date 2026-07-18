using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sheba.Identity.Domain.Interfaces;
using Sheba.Identity.Infrastructure.Adapters;

namespace Sheba.Identity.Tests.Infrastructure.Adapters;

public sealed class OpenCrvsNationalIdProviderTests
{
    private const string NationalId = "1000000001";
    private const string Phone = "0777000001";

    private static readonly OpenCrvsOptions Options = new()
    {
        GraphQlEndpoint = "https://opencrvs.test/graphql",
        TokenEndpoint = "https://opencrvs.test/token",
        ClientId = "sheba",
        ClientSecret = "secret",
        TimeoutSeconds = 5,
    };

    private static (OpenCrvsNationalIdProvider Sut, FakeOpenCrvsHandler Handler) Build()
    {
        var handler = new FakeOpenCrvsHandler();
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("OpenCrvs").Returns(_ => new HttpClient(handler));
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new OpenCrvsNationalIdProvider(
            factory, Microsoft.Extensions.Options.Options.Create(Options), cache,
            NullLogger<OpenCrvsNationalIdProvider>.Instance);
        return (sut, handler);
    }

    [Fact]
    public async Task LookupAsync_ActiveCitizen_MatchingPhone_ReturnsValid()
    {
        var (sut, handler) = Build();
        handler.CitizenResponse = CitizenJson(NationalId, Phone, "ACTIVE");

        var result = await sut.LookupAsync(NationalId, Phone, CancellationToken.None);

        result.IsFound.Should().BeTrue();
        result.Status.Should().Be(NidStatus.Valid);
        result.FullNameEn.Should().Be("Ahmed Al-Yemeni");
    }

    [Fact]
    public async Task LookupAsync_DeceasedCitizen_ReturnsDeceased()
    {
        var (sut, handler) = Build();
        handler.CitizenResponse = CitizenJson(NationalId, Phone, "DECEASED");

        var result = await sut.LookupAsync(NationalId, Phone, CancellationToken.None);

        result.IsFound.Should().BeTrue();
        result.Status.Should().Be(NidStatus.Deceased);
    }

    [Fact]
    public async Task LookupAsync_NoMatchingRecord_ReturnsNotFound()
    {
        var (sut, handler) = Build();
        handler.CitizenResponse = null;

        var result = await sut.LookupAsync(NationalId, Phone, CancellationToken.None);

        result.IsFound.Should().BeFalse();
        result.Status.Should().Be(NidStatus.NotFound);
    }

    [Fact]
    public async Task LookupAsync_PhoneMismatch_ReturnsNotFound_NotADistinctReason()
    {
        var (sut, handler) = Build();
        handler.CitizenResponse = CitizenJson(NationalId, "0777999999", "ACTIVE");

        var result = await sut.LookupAsync(NationalId, Phone, CancellationToken.None);

        result.IsFound.Should().BeFalse();
        result.Status.Should().Be(NidStatus.NotFound);
    }

    [Fact]
    public async Task LookupAsync_RegistryUnreachable_ThrowsInsteadOfReturningNotFound()
    {
        var (sut, handler) = Build();
        handler.ThrowOnSend = true;

        var act = () => sut.LookupAsync(NationalId, Phone, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task LookupAsync_NonSuccessStatusFromRegistry_Throws()
    {
        var (sut, handler) = Build();
        handler.GraphQlStatusCode = HttpStatusCode.InternalServerError;

        var act = () => sut.LookupAsync(NationalId, Phone, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task LookupAsync_TokenCached_SecondLookupDoesNotRequestNewToken()
    {
        var (sut, handler) = Build();
        handler.CitizenResponse = CitizenJson(NationalId, Phone, "ACTIVE");

        await sut.LookupAsync(NationalId, Phone, CancellationToken.None);
        await sut.LookupAsync(NationalId, Phone, CancellationToken.None);

        handler.TokenRequestCount.Should().Be(1);
        handler.GraphQlRequestCount.Should().Be(2);
    }

    private static string CitizenJson(string nationalId, string phone, string status) =>
        JsonSerializer.Serialize(new
        {
            nationalId,
            phoneNumber = phone,
            fullNameAr = "أحمد اليمني",
            fullNameEn = "Ahmed Al-Yemeni",
            dateOfBirth = "1990-03-15",
            gender = "M",
            status,
        });

    /// <summary>Hand-rolled HttpMessageHandler stub — this repo has no WireMock/HTTP-mocking package.</summary>
    private sealed class FakeOpenCrvsHandler : HttpMessageHandler
    {
        public string? CitizenResponse { get; set; }
        public HttpStatusCode GraphQlStatusCode { get; set; } = HttpStatusCode.OK;
        public bool ThrowOnSend { get; set; }
        public int TokenRequestCount { get; private set; }
        public int GraphQlRequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (ThrowOnSend)
                throw new HttpRequestException("simulated registry outage");

            if (request.RequestUri!.AbsoluteUri.Contains("/token"))
            {
                TokenRequestCount++;
                var body = """{"access_token":"fake-token","expires_in":3600}""";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
            }

            GraphQlRequestCount++;
            if (GraphQlStatusCode != HttpStatusCode.OK)
                return Task.FromResult(new HttpResponseMessage(GraphQlStatusCode)
                {
                    Content = new StringContent("upstream error", Encoding.UTF8, "text/plain")
                });

            var data = CitizenResponse is null
                ? """{"data":{"citizenIdentity":null}}"""
                : "{\"data\":{\"citizenIdentity\":" + CitizenResponse + "}}";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(data, Encoding.UTF8, "application/json")
            });
        }
    }
}
