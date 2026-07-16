using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Identity.Domain.Entities;

/// <summary>
/// Short-lived OTP record. Used for registration, login, password reset, etc.
/// The raw code is NEVER stored — only the Argon2id hash.
/// Purged on use or expiry (background cleanup job).
/// </summary>
public sealed class OtpRecord : BaseEntity
{
    public Guid AccountId { get; private set; }
    public OtpPurpose Purpose { get; private set; }
    public OtpChannel Channel { get; private set; }

    /// <summary>Argon2id hash of the 6-digit code. Never store plaintext.</summary>
    public string CodeHash { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public int AttemptCount { get; private set; }

    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    // Max attempts before the OTP is invalidated (security)
    private const int MaxAttempts = 3;

    // EF Core
    private OtpRecord() { }

    /// <summary>Creates a new OTP record (after the code has been generated and hashed).</summary>
    public static OtpRecord Create(
        Guid accountId,
        OtpPurpose purpose,
        OtpChannel channel,
        string codeHash,
        int ttlMinutes = 5,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return new OtpRecord
        {
            AccountId  = accountId,
            Purpose    = purpose,
            Channel    = channel,
            CodeHash   = codeHash,
            ExpiresAt  = DateTime.UtcNow.AddMinutes(ttlMinutes),
            IpAddress  = ipAddress,
            UserAgent  = userAgent
        };
    }

    /// <summary>Returns true if this OTP is still valid and unused.</summary>
    public bool IsActive() => UsedAt is null && DateTime.UtcNow < ExpiresAt && AttemptCount < MaxAttempts;

    /// <summary>Marks the OTP as used (on successful verification).</summary>
    public void MarkUsed()
    {
        if (!IsActive())
            throw new DomainException("OTP is expired, already used, or has exceeded attempt limit.");

        UsedAt = DateTime.UtcNow;
        Touch();
    }

    /// <summary>Records a failed verification attempt.</summary>
    public void RecordFailedAttempt()
    {
        if (!IsActive())
            throw new DomainException("OTP is no longer valid.");

        AttemptCount++;
        Touch();
    }

    /// <summary>Alias for handler use.</summary>
    public void RecordAttempt() => RecordFailedAttempt();

    public bool HasExceededAttempts() => AttemptCount >= MaxAttempts;
    public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;

    /// <summary>
    /// Immediately expires this OTP (used by InvalidatePreviousOtpsAsync).
    /// Idempotent — safe to call on already-expired records.
    /// </summary>
    public void Expire()
    {
        ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        Touch();
    }
}

