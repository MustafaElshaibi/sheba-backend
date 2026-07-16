using Microsoft.Extensions.Logging;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Notification.Infrastructure.Adapters;

/// <summary>
/// Development SMS/OTP provider for the Notification module.
/// Prints the SMS message to the structured log so developers can read it
/// without a real SMS gateway or Twilio account.
///
/// Architecture: Implements ISmsService from Sheba.Shared.Kernel.
/// Named ConsoleOtpProvider per the Sheba architecture specification (SHEBA_ARCHITECTURE.md).
/// Registered by NotificationModule when Notification:Sms:ActiveProvider = "Console".
///
/// Contrast with Identity.Infrastructure.Adapters.ConsoleOtpProvider which handles
/// the raw 6-digit OTP generation at the Identity layer (different responsibility).
/// This class handles generic SMS dispatch from the Notification layer.
/// </summary>
public sealed class ConsoleOtpProvider(
    ILogger<ConsoleOtpProvider> logger
) : ISmsService
{
    public Task<bool> SendAsync(
        string toPhoneNumber,
        string message,
        CancellationToken cancellationToken = default)
    {
        // In development: print the SMS so devs can read codes without Twilio
        logger.LogInformation(
            """
            ╔══════════════════════════════════════╗
            ║   📱 CONSOLE SMS — Notification Layer ║
            ╠══════════════════════════════════════╣
            ║ To:      {Phone}
            ║ Message: {Message}
            ╚══════════════════════════════════════╝
            """,
            toPhoneNumber, message);

        return Task.FromResult(true);
    }
}
