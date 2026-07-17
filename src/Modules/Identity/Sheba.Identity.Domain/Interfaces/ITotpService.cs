namespace Sheba.Identity.Domain.Interfaces;

/// <summary>
/// TOTP (RFC 6238) generation/verification port for admin MFA (T-SEC-1).
/// Implementation lives in Identity.Infrastructure (Otp.NET). The Application layer only sees
/// this interface, keeping the Clean Architecture boundary (Application → Kernel/Domain only).
/// </summary>
public interface ITotpService
{
    /// <summary>Generates a new random base32-encoded TOTP secret (160-bit).</summary>
    string GenerateSecret();

    /// <summary>Builds an otpauth:// provisioning URI for authenticator-app QR enrollment.</summary>
    string BuildProvisioningUri(string base32Secret, string accountLabel);

    /// <summary>Verifies a 6-digit code against the base32 secret, tolerating ±1 time step of drift.</summary>
    bool VerifyCode(string base32Secret, string code);
}
