using OtpNet;
using Sheba.Identity.Domain.Interfaces;

namespace Sheba.Identity.Infrastructure.Security;

/// <summary>
/// RFC 6238 TOTP via Otp.NET: 160-bit secret, SHA1, 6 digits, 30-second step (the parameters every
/// mainstream authenticator app — Google/Microsoft Authenticator, Authy — assumes by default).
/// Verification allows ±1 step of clock drift, matching the RFC's recommended tolerance.
/// </summary>
public sealed class OtpNetTotpService : ITotpService
{
    private const int SecretLengthBytes = 20; // 160 bits
    private const string Issuer = "Sheba";
    private static readonly VerificationWindow DriftWindow = new(previous: 1, future: 1);

    public string GenerateSecret() =>
        Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(SecretLengthBytes));

    public string BuildProvisioningUri(string base32Secret, string accountLabel)
    {
        var label = Uri.EscapeDataString($"{Issuer}:{accountLabel}");
        var issuer = Uri.EscapeDataString(Issuer);
        return $"otpauth://totp/{label}?secret={base32Secret}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";
    }

    public bool VerifyCode(string base32Secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        try
        {
            var totp = new Totp(Base32Encoding.ToBytes(base32Secret));
            return totp.VerifyTotp(code, out _, DriftWindow);
        }
        catch
        {
            return false;
        }
    }
}
