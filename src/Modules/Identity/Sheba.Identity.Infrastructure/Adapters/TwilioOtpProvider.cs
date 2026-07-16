using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Domain.Interfaces;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Rest.Verify.V2.Service;

namespace Sheba.Identity.Infrastructure.Adapters;

/// <summary>
/// Production OTP adapter using Twilio Verify API.
/// Active when: Otp:ActiveProvider = "Twilio"
///
/// Required configuration:
///   Twilio:AccountSid     — Twilio account SID
///   Twilio:AuthToken      — Twilio auth token
///   Twilio:VerifyServiceSid — Twilio Verify service SID (for managed OTP)
///   Twilio:FromNumber       — fallback: send raw SMS (dev only)
/// </summary>
public sealed class TwilioOtpProvider : IOtpProvider
{
    private readonly string _verifyServiceSid;
    private readonly ILogger<TwilioOtpProvider> _logger;

    public TwilioOtpProvider(IConfiguration configuration, ILogger<TwilioOtpProvider> logger)
    {
        _logger = logger;

        var accountSid = configuration["Twilio:AccountSid"]
            ?? throw new InvalidOperationException("Twilio:AccountSid is not configured.");
        var authToken = configuration["Twilio:AuthToken"]
            ?? throw new InvalidOperationException("Twilio:AuthToken is not configured.");

        _verifyServiceSid = configuration["Twilio:VerifyServiceSid"]
            ?? throw new InvalidOperationException("Twilio:VerifyServiceSid is not configured.");

        TwilioClient.Init(accountSid, authToken);
    }

    public async Task<(OtpSendResult Result, string RawCode)> SendAsync(
        string destination,
        OtpPurpose purpose,
        OtpChannel channel,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var twilioChannel = channel == OtpChannel.Email ? "email" : "sms";

            _logger.LogInformation(
                "[TwilioOTP] Sending OTP to {Destination} via {Channel} for {Purpose}",
                destination, twilioChannel, purpose);

            // Twilio Verify manages its own codes — we don't get the raw code back.
            // Store a sentinel value in the DB to indicate "Twilio manages verification".
            var verification = await VerificationResource.CreateAsync(
                to: destination,
                channel: twilioChannel,
                pathServiceSid: _verifyServiceSid);

            _logger.LogInformation(
                "[TwilioOTP] OTP sent. Verification SID={Sid} Status={Status}",
                verification.Sid, verification.Status);

            // Return sentinel — the application layer should detect "TWILIO_MANAGED"
            // and skip the local code-hash check, delegating to VerifyAsync instead.
            return (new OtpSendResult(Succeeded: true), "TWILIO_MANAGED");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TwilioOTP] Failed to send OTP to {Destination}", destination);
            return (new OtpSendResult(Succeeded: false, ex.Message), string.Empty);
        }
    }

    public async Task<OtpVerifyResult> VerifyAsync(
        string destination,
        string code,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var check = await VerificationCheckResource.CreateAsync(
                to: destination,
                code: code,
                pathServiceSid: _verifyServiceSid);

            var isValid = string.Equals(check.Status, "approved", StringComparison.OrdinalIgnoreCase);

            _logger.LogInformation(
                "[TwilioOTP] Verification check for {Destination}: {Status}", destination, check.Status);

            return new OtpVerifyResult(IsValid: isValid, isValid ? null : "OTP code is invalid or expired.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TwilioOTP] Verification check failed for {Destination}", destination);
            return new OtpVerifyResult(IsValid: false, ex.Message);
        }
    }
}
