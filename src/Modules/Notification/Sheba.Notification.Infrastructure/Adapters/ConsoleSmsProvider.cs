using Microsoft.Extensions.Logging;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Notification.Infrastructure.Adapters;

/// <summary>
/// Development SMS provider: prints the OTP/SMS message to the console log.
/// Used in development so you can see SMS content without an actual SMS gateway.
///
/// Architecture: Implements ISmsService from Sheba.Shared.Kernel.
/// Registered by NotificationModule when Notification:Sms:ActiveProvider = "Console".
///
/// Note: This provider is in Notification.Infrastructure, separate from
/// Identity.Infrastructure.Adapters.ConsoleOtpProvider (which handles OTP sending
/// at the Identity level). They serve the same dev purpose but are in different modules
/// per the modular monolith boundary rule.
/// </summary>
public sealed class ConsoleSmsProvider(
    ILogger<ConsoleSmsProvider> logger
) : ISmsService
{
    public Task<bool> SendAsync(
        string toPhoneNumber,
        string message,
        CancellationToken cancellationToken = default)
    {
        // In development, print the SMS content so developers can use OTPs without Twilio
        logger.LogInformation(
            """
            ╔══════════════════════════════════════╗
            ║   📱 CONSOLE SMS (dev only)           ║
            ╠══════════════════════════════════════╣
            ║ To:      {Phone}
            ║ Message: {Message}
            ╚══════════════════════════════════════╝
            """,
            toPhoneNumber, message);

        return Task.FromResult(true);
    }
}
