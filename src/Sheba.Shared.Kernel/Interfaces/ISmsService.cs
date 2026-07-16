namespace Sheba.Shared.Kernel.Interfaces;

/// <summary>
/// Shared SMS dispatch abstraction.
///
/// Placed in Shared.Kernel so Identity.Application and other modules can inject it
/// without depending on Notification.Infrastructure directly.
///
/// Implementations live in Notification.Infrastructure:
///   • ConsoleSmsProvider  — dev (prints to console)
///   • TwilioSmsProvider   — production
///
/// Switched via configuration key Notification:Sms:ActiveProvider.
/// </summary>
public interface ISmsService
{
    /// <summary>
    /// Sends an SMS. Returns true if the gateway accepted the message.
    /// Never throws on transport failure — catches and returns false.
    /// </summary>
    Task<bool> SendAsync(
        string toPhoneNumber,
        string message,
        CancellationToken cancellationToken = default);
}
