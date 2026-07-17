using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Ministry.Domain.Entities;
using Sheba.Ministry.Domain.Interfaces;
using Sheba.Ministry.Infrastructure.Security;
using StackExchange.Redis;

namespace Sheba.Ministry.Tests.Security;

/// <summary>
/// Tests the inbound-webhook verification contract (§7.4 / BR-MI-5): constant-time HMAC signature,
/// ±5-minute timestamp window, and delivery-id dedup. These are the gate that stops a forged or
/// replayed ministry callback from advancing a citizen's service request, so every rejection path
/// is asserted explicitly.
/// </summary>
public sealed class MinistryWebhookVerifierTests
{
    private const string Secret = "super-secret-signing-key";
    private const string EventType = "request.completed";
    private static readonly Guid MinistryId = Guid.NewGuid();

    private readonly IMinistryRepository _repo = Substitute.For<IMinistryRepository>();
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _db = Substitute.For<IDatabase>();
    private readonly MinistryWebhookVerifier _sut;

    public MinistryWebhookVerifierTests()
    {
        _redis.GetDatabase().ReturnsForAnyArgs(_db);
        // Default: delivery id is new (first delivery).
        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<When>())
           .ReturnsForAnyArgs(true);

        _sut = new MinistryWebhookVerifier(
            _repo, new IdentityEncryptor(), _redis, NullLogger<MinistryWebhookVerifier>.Instance);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private void SeedActiveWebhook(string secret = Secret) =>
        _repo.GetWebhooksByMinistryAsync(MinistryId, Arg.Any<CancellationToken>())
             .Returns(new List<MinistryWebhook>
             {
                 // Stored "encrypted" — the identity encryptor keeps it as plaintext for the test.
                 MinistryWebhook.Create(MinistryId, EventType, "/api/webhooks/ministry", secret)
             });

    private static string Sign(string secret, string ts, string body) =>
        Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{ts}.{body}")));

    private static string NowTs() => DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

    // ── tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyAsync_ValidSignatureTimestampAndFirstDelivery_ReturnsValid()
    {
        SeedActiveWebhook();
        var ts = NowTs();
        var body = """{"requestId":"abc"}""";

        var result = await _sut.VerifyAsync(
            MinistryId, EventType, body, Sign(Secret, ts, body), ts, Guid.NewGuid().ToString());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_TamperedBody_ReturnsInvalidSignature()
    {
        SeedActiveWebhook();
        var ts = NowTs();
        var signature = Sign(Secret, ts, """{"amount":10}""");

        // Same signature, different body → HMAC must not match.
        var result = await _sut.VerifyAsync(
            MinistryId, EventType, """{"amount":9999}""", signature, ts, Guid.NewGuid().ToString());

        result.Status.Should().Be(WebhookVerificationStatus.InvalidSignature);
    }

    [Fact]
    public async Task VerifyAsync_SignedWithWrongSecret_ReturnsInvalidSignature()
    {
        SeedActiveWebhook();
        var ts = NowTs();
        var body = """{"ok":true}""";

        var result = await _sut.VerifyAsync(
            MinistryId, EventType, body, Sign("attacker-secret", ts, body), ts, Guid.NewGuid().ToString());

        result.Status.Should().Be(WebhookVerificationStatus.InvalidSignature);
    }

    [Fact]
    public async Task VerifyAsync_StaleTimestamp_ReturnsStaleTimestamp()
    {
        SeedActiveWebhook();
        var staleTs = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 600).ToString(); // 10 min old
        var body = """{"ok":true}""";

        // Signature is valid over the stale timestamp, so it passes the HMAC check and is then
        // rejected purely on freshness.
        var result = await _sut.VerifyAsync(
            MinistryId, EventType, body, Sign(Secret, staleTs, body), staleTs, Guid.NewGuid().ToString());

        result.Status.Should().Be(WebhookVerificationStatus.StaleTimestamp);
    }

    [Fact]
    public async Task VerifyAsync_ReplayedDeliveryId_ReturnsDuplicateDelivery()
    {
        SeedActiveWebhook();
        // Redis reports the key already existed → replay.
        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<When>())
           .ReturnsForAnyArgs(false);
        var ts = NowTs();
        var body = """{"ok":true}""";

        var result = await _sut.VerifyAsync(
            MinistryId, EventType, body, Sign(Secret, ts, body), ts, "delivery-1");

        result.Status.Should().Be(WebhookVerificationStatus.DuplicateDelivery);
    }

    [Fact]
    public async Task VerifyAsync_NoActiveWebhook_ReturnsNoWebhookConfigured()
    {
        _repo.GetWebhooksByMinistryAsync(MinistryId, Arg.Any<CancellationToken>())
             .Returns(new List<MinistryWebhook>());
        var ts = NowTs();

        var result = await _sut.VerifyAsync(
            MinistryId, EventType, "{}", Sign(Secret, ts, "{}"), ts, "d1");

        result.Status.Should().Be(WebhookVerificationStatus.NoWebhookConfigured);
    }

    [Fact]
    public async Task VerifyAsync_MissingHeaders_ReturnsMalformed()
    {
        SeedActiveWebhook();

        var result = await _sut.VerifyAsync(
            MinistryId, EventType, "{}", signatureHex: null, timestamp: null, deliveryId: null);

        result.Status.Should().Be(WebhookVerificationStatus.Malformed);
    }

    private sealed class IdentityEncryptor : ICredentialEncryptor
    {
        public string Encrypt(string plaintext) => plaintext;
        public string Decrypt(string ciphertext) => ciphertext;
    }
}
