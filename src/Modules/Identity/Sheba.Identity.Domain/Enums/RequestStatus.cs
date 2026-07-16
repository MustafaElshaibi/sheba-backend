namespace Sheba.Identity.Domain.Enums;

/// <summary>
/// Status of a digital identity (eKYC) request in the admin review workflow.
/// </summary>
public enum RequestStatus
{
    /// <summary>Submitted by citizen; not yet picked up by a reviewer.</summary>
    Pending = 1,

    /// <summary>Reviewer has opened the request but not yet decided.</summary>
    UnderReview = 2,

    /// <summary>Admin approved — triggers account activation.</summary>
    Approved = 3,

    /// <summary>Admin rejected — triggers rejection notification.</summary>
    Rejected = 4,

    /// <summary>Citizen withdrew the request before a decision was made.</summary>
    Cancelled = 5
}

/// <summary>
/// The type of identity request being submitted.
/// </summary>
public enum RequestType
{
    /// <summary>First-time account opening.</summary>
    OpenAccount = 1,

    /// <summary>Upgrade from LoA1 to LoA2 (requires KYC documents).</summary>
    UpgradeLoa2 = 2,

    /// <summary>Upgrade to LoA3 (requires biometric / in-person verification).</summary>
    UpgradeLoa3 = 3,

    /// <summary>Reopen a previously deactivated account.</summary>
    ReopenAccount = 4
}

/// <summary>
/// Channel via which the OTP is delivered.
/// </summary>
public enum OtpChannel
{
    Sms = 1,
    Email = 2,
    Totp = 3  // Admin TOTP (Otp.NET)
}

/// <summary>
/// The reason the OTP was generated.
/// </summary>
public enum OtpPurpose
{
    Login = 1,
    Registration = 2,
    PasswordReset = 3,
    EmailVerify = 4,
    PhoneVerify = 5,
    SensitiveAction = 6,
    TransactionConfirm = 7
}
