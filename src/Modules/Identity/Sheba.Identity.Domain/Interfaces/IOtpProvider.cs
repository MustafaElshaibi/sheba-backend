using Sheba.Identity.Domain.Enums;

namespace Sheba.Identity.Domain.Interfaces;

/// <summary>
/// Result returned after sending and verifying a one-time password.
/// </summary>
public sealed record OtpSendResult(bool Succeeded, string? ErrorMessage = null);
public sealed record OtpVerifyResult(bool IsValid, string? ErrorMessage = null);

/// <summary>
/// Port (interface) for OTP generation and delivery.
/// Development: ConsoleOtpProvider  |  Production: TwilioOtpProvider
/// Switched via configuration key Otp:ActiveProvider.
/// </summary>
public interface IOtpProvider
{
    /// <summary>
    /// Generates a 6-digit OTP and sends it via the configured channel.
    /// Returns the raw code so the caller can hash and store it.
    /// </summary>
    Task<(OtpSendResult Result, string RawCode)> SendAsync(
        string destination,
        OtpPurpose purpose,
        OtpChannel channel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies an OTP against the stored hash (done at application layer).
    /// The provider itself is stateless — verification is DB-backed.
    /// This method exists for providers that do their own verification (e.g. Twilio Verify).
    /// </summary>
    Task<OtpVerifyResult> VerifyAsync(
        string destination,
        string code,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default);
}
