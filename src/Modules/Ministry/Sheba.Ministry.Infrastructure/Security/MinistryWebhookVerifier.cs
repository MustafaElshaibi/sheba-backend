using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Sheba.Ministry.Domain.Interfaces;
using StackExchange.Redis;

namespace Sheba.Ministry.Infrastructure.Security;

/// <summary>
/// Implements the Sheba inbound-webhook verification contract (§7.4). See
/// <see cref="IMinistryWebhookVerifier"/> for the boundary rationale.
///
/// Signature scheme: <c>HMAC-SHA256(signing_secret, timestamp + "." + raw_body)</c>, hex-encoded,
/// compared in constant time. The signing secret is stored AES-256-GCM encrypted and decrypted
/// only here, at verification time.
///
/// Replay protection has two layers: the timestamp window bounds how long any single (valid)
/// signature can be replayed, and the delivery-id dedup store rejects repeats inside that window.
/// The dedup store is Redis (SET NX with a TTL comfortably larger than the window) rather than a
/// DB table — replay keys are inherently short-lived, so this avoids a schema/migration for them
/// and puts the already-provisioned Redis to use. If Redis is unreachable we fail open on *dedup
/// only* (log a warning); signature + timestamp still gate the callback.
/// </summary>
public sealed class MinistryWebhookVerifier(
    IMinistryRepository ministryRepository,
    ICredentialEncryptor encryptor,
    IConnectionMultiplexer redis,
    ILogger<MinistryWebhookVerifier> logger) : IMinistryWebhookVerifier
{
    private const int TimestampWindowSeconds = 300;              // ±5 minutes (§7.4)
    private static readonly TimeSpan DedupTtl = TimeSpan.FromSeconds(TimestampWindowSeconds * 2 + 60);

    public async Task<WebhookVerificationResult> VerifyAsync(
        Guid ministryId,
        string? eventType,
        string rawBody,
        string? signatureHex,
        string? timestamp,
        string? deliveryId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(signatureHex) ||
            string.IsNullOrWhiteSpace(timestamp) ||
            string.IsNullOrWhiteSpace(deliveryId))
        {
            return Reject(WebhookVerificationStatus.Malformed, ministryId,
                "missing signature, timestamp, or delivery id");
        }

        // ── Resolve the signing secret ────────────────────────────────────────
        // Match the registered active webhook by event type; if the ministry has exactly one
        // active webhook, event type is optional. No match → nothing to verify against.
        var webhooks = await ministryRepository.GetWebhooksByMinistryAsync(ministryId, ct);
        var active = webhooks.Where(w => w.IsActive).ToList();
        var webhook = !string.IsNullOrWhiteSpace(eventType)
            ? active.FirstOrDefault(w => string.Equals(w.EventType, eventType, StringComparison.OrdinalIgnoreCase))
            : active.Count == 1 ? active[0] : null;

        if (webhook is null)
            return Reject(WebhookVerificationStatus.NoWebhookConfigured, ministryId,
                $"no active webhook for event '{eventType}'");

        string signingSecret;
        try
        {
            signingSecret = encryptor.Decrypt(webhook.SigningSecret);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Webhook] Failed to decrypt signing secret for Ministry {MinistryId}", ministryId);
            return new WebhookVerificationResult(WebhookVerificationStatus.NoWebhookConfigured, webhook.Id);
        }

        // ── 1. Signature (constant-time) ──────────────────────────────────────
        byte[] providedSignature;
        try
        {
            providedSignature = Convert.FromHexString(signatureHex);
        }
        catch (FormatException)
        {
            return Reject(WebhookVerificationStatus.Malformed, ministryId, "signature is not valid hex");
        }

        var expected = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(signingSecret),
            Encoding.UTF8.GetBytes($"{timestamp}.{rawBody}"));

        if (!CryptographicOperations.FixedTimeEquals(providedSignature, expected))
            return Reject(WebhookVerificationStatus.InvalidSignature, ministryId, "HMAC mismatch");

        // ── 2. Timestamp window ───────────────────────────────────────────────
        if (!long.TryParse(timestamp, out var unixSeconds))
            return Reject(WebhookVerificationStatus.Malformed, ministryId, "timestamp is not a unix epoch");

        var age = Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - unixSeconds);
        if (age > TimestampWindowSeconds)
            return Reject(WebhookVerificationStatus.StaleTimestamp, ministryId, $"timestamp {age}s outside window");

        // ── 3. Delivery-id dedup ──────────────────────────────────────────────
        if (await IsReplayAsync(ministryId, deliveryId!))
            return Reject(WebhookVerificationStatus.DuplicateDelivery, ministryId, $"delivery id {deliveryId} replayed");

        return new WebhookVerificationResult(WebhookVerificationStatus.Valid, webhook.Id);
    }

    /// <summary>Records the delivery id and returns true if it was already present (a replay).</summary>
    private async Task<bool> IsReplayAsync(Guid ministryId, string deliveryId)
    {
        try
        {
            var db = redis.GetDatabase();
            var key = $"webhook:dedup:{ministryId}:{deliveryId}";
            // SET NX: succeeds (true) only if the key did not exist → first delivery.
            var isFirst = await db.StringSetAsync(key, "1", DedupTtl, when: When.NotExists);
            return !isFirst;
        }
        catch (Exception ex)
        {
            // Fail open on dedup only — signature + timestamp already bound the replay surface.
            logger.LogWarning(ex, "[Webhook] Redis dedup unavailable; skipping delivery-id check for Ministry {MinistryId}", ministryId);
            return false;
        }
    }

    private WebhookVerificationResult Reject(WebhookVerificationStatus status, Guid ministryId, string detail)
    {
        // Store-and-alert intent (§7.4): failed receipts are surfaced loudly for investigation.
        // Never log the raw body or secret — only the reason and the ministry id.
        logger.LogWarning("[Webhook] Rejected callback for Ministry {MinistryId}: {Status} — {Detail}",
            ministryId, status, detail);
        return new WebhookVerificationResult(status);
    }
}
