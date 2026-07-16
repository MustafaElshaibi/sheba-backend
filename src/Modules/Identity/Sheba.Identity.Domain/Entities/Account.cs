using Sheba.Identity.Domain.DomainEvents;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Identity.Domain.Entities;

/// <summary>
/// Core citizen digital identity account.
/// Lifecycle: PendingVerification → PendingAdminApproval → Approved / Rejected
/// </summary>
public sealed class Account : BaseEntity
{
    // ── Identity fields (from civil registry) ─────────────────────────────────
    public string NationalId { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string FullNameAr { get; private set; } = string.Empty;
    public string FullNameEn { get; private set; } = string.Empty;

    // ── Account credentials (entered by citizen) ──────────────────────────────
    public string Username { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public DateTime? EmailVerifiedAt { get; private set; }
    public DateTime? PhoneVerifiedAt { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;

    // ── Status & level of assurance ───────────────────────────────────────────
    public AccountStatus Status { get; private set; } = AccountStatus.PendingVerification;
    public int IdentityLevel { get; private set; } = 1;

    // ── Security counters ─────────────────────────────────────────────────────
    public int FailedLoginCount { get; private set; }
    public DateTime? LockedUntil { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    // EF Core requires a parameterless constructor
    private Account() { }

    /// <summary>
    /// Factory: creates a new account after NID check passes.
    /// Status is PendingVerification until OTP is confirmed.
    /// </summary>
    public static Account CreateFromNidCheck(
        string nationalId,
        string phoneNumber,
        string fullNameAr,
        string fullNameEn)
    {
        var account = new Account
        {
            NationalId  = nationalId,
            PhoneNumber = phoneNumber,
            FullNameAr  = fullNameAr,
            FullNameEn  = fullNameEn,
            Status      = AccountStatus.PendingVerification
        };

        account.RaiseDomainEvent(new AccountRegisteredEvent(account.Id, nationalId));
        return account;
    }

    /// <summary>
    /// Completes registration after OTP is verified and citizen sets credentials.
    /// Moves to PendingEmailVerification — an email verification link is sent.
    /// </summary>
    public void CompleteRegistration(string username, string email, string passwordHash)
    {
        if (Status != AccountStatus.PendingVerification)
            throw new DomainException("Registration can only be completed from PendingVerification status.");

        Username     = username;
        Email        = email;
        PasswordHash = passwordHash;
        Status       = AccountStatus.PendingEmailVerification;
        Touch();
    }

    public void VerifyPhone()
    {
        PhoneVerifiedAt = DateTime.UtcNow;
        Touch();
    }

    public void MarkPhoneVerified() => VerifyPhone(); // alias for backward compat

    public void MarkEmailVerified()
    {
        if (Status != AccountStatus.PendingEmailVerification)
            throw new DomainException($"Cannot verify email in status {Status}. Expected PendingEmailVerification.");

        EmailVerifiedAt = DateTime.UtcNow;
        Status = AccountStatus.PendingAdminApproval;
        Touch();
    }

    /// <summary>Admin approves — activates the account.</summary>
    public void Approve()
    {
        if (Status != AccountStatus.PendingAdminApproval)
            throw new DomainException($"Cannot approve account in status {Status}.");

        Status = AccountStatus.Approved;
        Touch();
    }

    /// <summary>Admin rejects the request. Alias with rejection reason param.</summary>
    public void Reject(string? rejectionReason = null)
    {
        if (Status != AccountStatus.PendingAdminApproval)
            throw new DomainException($"Cannot reject account in status {Status}.");

        Status = AccountStatus.Rejected;
        Touch();
    }

    /// <summary>Records a failed login attempt; locks after 5 failures.</summary>
    public void RecordFailedLogin()
    {
        FailedLoginCount++;
        if (FailedLoginCount >= 5)
            LockedUntil = DateTime.UtcNow.AddMinutes(Math.Pow(2, FailedLoginCount - 4));
        Touch();
    }

    /// <summary>Records a successful login; resets failure counters.</summary>
    public void RecordSuccessfulLogin()
    {
        FailedLoginCount = 0;
        LockedUntil      = null;
        LastLoginAt      = DateTime.UtcNow;
        Touch();
    }

    /// <summary>Sets citizen-supplied credentials and advances status to PendingEmailVerification.</summary>
    public void SetCredentials(string username, string email, string passwordHash)
    {
        Username     = username;
        Email        = email;
        PasswordHash = passwordHash;
        Status       = AccountStatus.PendingEmailVerification;
        Touch();
    }

    /// <summary>Raises the account's Level of Assurance after an approved upgrade request.</summary>
    public void UpgradeIdentityLevel(int newLevel)
    {
        if (Status != AccountStatus.Approved)
            throw new DomainException("Only an active account can have its LoA upgraded.");
        if (newLevel is not (2 or 3))
            throw new DomainException("LoA level must be 2 or 3.");
        if (newLevel <= IdentityLevel)
            throw new DomainException($"Account is already at LoA {IdentityLevel} or higher.");

        IdentityLevel = newLevel;
        Touch();
    }

    public bool IsLocked() => LockedUntil.HasValue && LockedUntil > DateTime.UtcNow;
}
