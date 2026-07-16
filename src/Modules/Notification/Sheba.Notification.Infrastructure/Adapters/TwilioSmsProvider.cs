using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sheba.Shared.Kernel.Interfaces;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace Sheba.Notification.Infrastructure.Adapters;

/// <summary>
/// Production SMS provider via Twilio Programmable Messaging.
///
/// Configuration keys (appsettings.json / secrets):
///   Notification:Sms:Twilio:AccountSid → "ACxxxxxxxxxxxxxxxxxx"
///   Notification:Sms:Twilio:AuthToken  → (from user secrets / environment)
///   Notification:Sms:Twilio:FromNumber → "+12025551234"
///
/// Implements ISmsService from Sheba.Shared.Kernel.
/// Registered by NotificationModule when Notification:Sms:ActiveProvider = "Twilio".
/// </summary>
public sealed class TwilioSmsProvider(
    IConfiguration configuration,
    ILogger<TwilioSmsProvider> logger
) : ISmsService
{
    private string AccountSid  => configuration["Notification:Sms:Twilio:AccountSid"] ?? string.Empty;
    private string AuthToken   => configuration["Notification:Sms:Twilio:AuthToken"]  ?? string.Empty;
    private string FromNumber  => configuration["Notification:Sms:Twilio:FromNumber"] ?? string.Empty;

    public async Task<bool> SendAsync(
        string toPhoneNumber,
        string message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            TwilioClient.Init(AccountSid, AuthToken);

            var msg = await MessageResource.CreateAsync(
                to:   new Twilio.Types.PhoneNumber(toPhoneNumber),
                from: new Twilio.Types.PhoneNumber(FromNumber),
                body: message);

            logger.LogInformation(
                "[TwilioSms] SMS sent to {Phone}: SID={Sid}, Status={Status}",
                toPhoneNumber, msg.Sid, msg.Status);

            // Twilio "queued" / "sent" / "delivered" are all success states
            return msg.ErrorCode is null;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[TwilioSms] Failed to send SMS to {Phone}",
                toPhoneNumber);
            return false;
        }
    }
}
