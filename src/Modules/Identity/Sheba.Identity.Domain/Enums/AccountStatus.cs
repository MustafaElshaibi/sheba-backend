namespace Sheba.Identity.Domain.Enums;

/// <summary>
/// Lifecycle status of a citizen's digital identity account.
/// </summary>
public enum AccountStatus
{
    /// <summary>NID/phone submitted; awaiting OTP verification.</summary>
    PendingVerification = 1,

    /// <summary>Phone verified; awaiting email verification.</summary>
    PendingEmailVerification = 2,

    /// <summary>Email verified; account details submitted; awaiting admin review.</summary>
    PendingAdminApproval = 3,

    /// <summary>Admin approved — account is fully active.</summary>
    Approved = 4,

    /// <summary>Admin rejected — citizen notified with reason.</summary>
    Rejected = 5,

    /// <summary>Active account temporarily disabled (e.g. security hold).</summary>
    Suspended = 6,

    /// <summary>Permanently closed — cannot be reactivated.</summary>
    Deactivated = 7
}
