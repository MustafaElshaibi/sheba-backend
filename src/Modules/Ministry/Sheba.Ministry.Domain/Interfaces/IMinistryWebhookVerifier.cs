namespace Sheba.Ministry.Domain.Interfaces;

/// <summary>
/// Outcome of verifying an inbound ministry webhook. Anything other than <see cref="Valid"/>
/// means the callback must be rejected and never processed (§7.4). The distinct reasons exist
/// for logging/alerting, not to be echoed back to the caller in detail.
/// </summary>
public enum WebhookVerificationStatus
{
    Valid,
    NoWebhookConfigured,   // no active webhook registered for this ministry/event
    Malformed,             // missing/undecodable signature, timestamp, or delivery id
    InvalidSignature,      // HMAC mismatch (possible forgery)
    StaleTimestamp,        // outside the ±5 min replay window
    DuplicateDelivery      // delivery id already seen (replay)
}

public sealed record WebhookVerificationResult(WebhookVerificationStatus Status, Guid? WebhookId = null)
{
    public bool IsValid => Status == WebhookVerificationStatus.Valid;
}

/// <summary>
/// Verifies inbound ministry webhooks per the Sheba webhook contract (§7.4): constant-time
/// HMAC-SHA256 signature check, a ±5-minute timestamp window, and delivery-id de-duplication.
///
/// This is a Ministry-owned port because the module owns the per-ministry signing secret (stored
/// encrypted) and the credential encryptor. ServiceRequest — which receives the HTTP callback —
/// depends only on this interface, keeping the secret and the crypto inside the Ministry boundary.
///
/// Verification order is fixed: signature → timestamp window → delivery-id dedup → (caller
/// proceeds). The timestamp is part of the signed payload, so it is read first but its freshness
/// is only trusted after the signature proves it was not tampered with.
/// </summary>
public interface IMinistryWebhookVerifier
{
    Task<WebhookVerificationResult> VerifyAsync(
        Guid ministryId,
        string? eventType,
        string rawBody,
        string? signatureHex,
        string? timestamp,
        string? deliveryId,
        CancellationToken ct = default);
}
