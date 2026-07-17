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
    /// Delivers an already-generated OTP <paramref name="code"/> via the configured channel.
    /// The provider is purely a delivery mechanism (§6.6) — it never generates, chooses, or
    /// knows a code beyond what it's handed here; generation and hashing are the Application
    /// layer's responsibility (<c>IOtpCodeGenerator</c> / <c>IOtpHasher</c> in Shared.Kernel).
    /// </summary>
    Task<OtpSendResult> SendAsync(
        string destination,
        string code,
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
