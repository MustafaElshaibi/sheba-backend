using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Identity.Domain.Entities;

public sealed class AdminUser : BaseEntity
{
    public string EmployeeId { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public AdminRole Role { get; private set; }
    public string? Department { get; private set; }
    public string Status { get; private set; } = "ACTIVE";
    public string PasswordHash { get; private set; } = string.Empty;
    public string? MfaSecret { get; private set; }
    public bool MfaEnabled { get; private set; }
    public int MfaFailedAttempts { get; private set; }
    public DateTime? MfaLockedUntil { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    /// <summary>Logical FK to a Ministry row (T-AUTH-1) — bare Guid per the module-boundary rule,
    /// never validated against the Ministry schema directly. Required for MinistryManager (that
    /// role's whole purpose is managing exactly one ministry's integration); null for every other
    /// role, which are either unrestricted (SuperAdmin) or not ministry-scoped by nature
    /// (IdentityReviewer, Auditor, Support).</summary>
    public Guid? MinistryId { get; private set; }

    private AdminUser() { }

    public static AdminUser Create(
        string employeeId,
        string email,
        string fullName,
        AdminRole role,
        string passwordHash,
        string? department = null,
        Guid? ministryId = null)
    {
        if (role == AdminRole.MinistryManager && ministryId is null)
            throw new DomainException("A MinistryManager must be scoped to a ministry.");
        if (role != AdminRole.MinistryManager && ministryId is not null)
            throw new DomainException($"{role} admins are not ministry-scoped.");

        var admin = new AdminUser
        {
            EmployeeId = employeeId,
            Email = email,
            FullName = fullName,
            Role = role,
            Department = department,
            PasswordHash = passwordHash,
            MinistryId = ministryId
        };

        return admin;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        Touch();
    }

    /// <summary>
    /// Begins (or restarts) TOTP enrollment — stores the encrypted secret but leaves MFA
    /// unenforced until <see cref="ConfirmMfaEnrollment"/> proves the admin's authenticator app
    /// actually has it (otherwise a typo'd QR scan could lock the admin out permanently).
    /// </summary>
    public void SetMfaSecret(string encryptedSecret)
    {
        if (MfaEnabled)
            throw new DomainException("MFA is already enabled. Disable it before re-enrolling.");

        MfaSecret = encryptedSecret;
        Touch();
    }

    /// <summary>Confirms enrollment after the admin proves possession with a valid TOTP code.</summary>
    public void ConfirmMfaEnrollment()
    {
        if (MfaSecret is null)
            throw new DomainException("No MFA enrollment is in progress for this admin.");
        if (MfaEnabled)
            throw new DomainException("MFA is already enabled.");

        MfaEnabled = true;
        MfaFailedAttempts = 0;
        MfaLockedUntil = null;
        Touch();
    }

    public bool IsMfaLocked() => MfaLockedUntil.HasValue && MfaLockedUntil > DateTime.UtcNow;

    /// <summary>Records a failed TOTP/recovery-code attempt; locks after 5 (mirrors Account's
    /// password lockout in BR-LG-3 — the same brute-force math applies to a 6-digit code).</summary>
    public void RecordFailedMfaAttempt()
    {
        MfaFailedAttempts++;
        if (MfaFailedAttempts >= 5)
            MfaLockedUntil = DateTime.UtcNow.AddMinutes(Math.Pow(2, MfaFailedAttempts - 4));
        Touch();
    }

    /// <summary>Resets MFA failure counters after a successful TOTP/recovery-code verification.</summary>
    public void ResetMfaFailures()
    {
        MfaFailedAttempts = 0;
        MfaLockedUntil = null;
        Touch();
    }

    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        Touch();
    }

    public void Deactivate()
    {
        if (Status is "DEACTIVATED")
            throw new DomainException("Admin user is already deactivated.");

        Status = "DEACTIVATED";
        Touch();
    }

    public void Suspend()
    {
        if (Status is not "ACTIVE")
            throw new DomainException("Only active admin users can be suspended.");

        Status = "SUSPENDED";
        Touch();
    }

    public void Reactivate()
    {
        if (Status is "ACTIVE")
            throw new DomainException("Admin user is already active.");

        Status = "ACTIVE";
        Touch();
    }
}