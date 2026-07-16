namespace Sheba.Notification.Domain.Interfaces;

/// <summary>
/// Port for SMS / OTP dispatch adapter.
/// Implementations: ConsoleOtpProvider (dev), TwilioSmsProvider (production).
/// Switched via configuration key Notification:Sms:ActiveProvider.
/// </summary>
public interface ISmsService
{
    /// <summary>
    /// Sends an SMS message to a phone number.
    /// Returns true if the message was accepted by the SMS gateway.
    /// Never throws on infrastructure failure — returns false and logs instead.
    /// </summary>
    Task<bool> SendAsync(
        string toPhoneNumber,
        string message,
        CancellationToken cancellationToken = default);
}
