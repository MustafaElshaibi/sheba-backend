using Microsoft.Extensions.Logging;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Domain.Interfaces;

namespace Sheba.Identity.Infrastructure.Adapters;

/// <summary>
/// Development OTP provider: prints the generated code to the console/Seq log instead of sending SMS.
/// Active when: Otp:ActiveProvider = "Console"
///
/// The provider is stateless — it only generates the raw code.
/// Hashing and storage happen in the application command handler.
/// </summary>
public sealed class ConsoleOtpProvider(ILogger<ConsoleOtpProvider> logger) : IOtpProvider
{
    private static readonly Random _random = new();

    public Task<(OtpSendResult Result, string RawCode)> SendAsync(
        string destination,
        OtpPurpose purpose,
        OtpChannel channel,
        CancellationToken cancellationToken = default)
    {
        var code = _random.Next(100_000, 999_999).ToString("D6");

        // Print clearly in development console so devs can read it
        logger.LogWarning(
            """
            ╔══════════════════════════════════════════════╗
            ║  [DEV OTP] DO NOT USE IN PRODUCTION         ║
            ║  Destination : {Destination,-28}   ║
            ║  Purpose     : {Purpose,-28}   ║
            ║  Channel     : {Channel,-28}   ║
            ║  CODE        : {Code,-28}   ║
            ╚══════════════════════════════════════════════╝
            """,
            destination, purpose, channel, code);

        return Task.FromResult((new OtpSendResult(Succeeded: true), code));
    }

    public Task<OtpVerifyResult> VerifyAsync(
        string destination,
        string code,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        // ConsoleOtpProvider does not manage its own state.
        // Verification is always delegated to the application layer (DB-backed hash check).
        return Task.FromResult(new OtpVerifyResult(IsValid: true));
    }
}
