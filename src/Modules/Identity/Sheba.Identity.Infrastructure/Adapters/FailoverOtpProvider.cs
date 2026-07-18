using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Domain.Interfaces;

namespace Sheba.Identity.Infrastructure.Adapters;

/// <summary>
/// Composite <see cref="IOtpProvider"/> (T-INT-2) that tries an ordered list of delivery providers
/// until one succeeds. Order comes from config <c>Otp:FailoverOrder</c> (e.g. ["Twilio","Console"]);
/// each entry resolves a keyed <see cref="IOtpProvider"/>. Every attempt and any full exhaustion is
/// reported to <see cref="IOtpSpendAlarm"/> for alerting. A provider that throws is treated as a
/// failed attempt and the chain continues, so one flaky gateway never blocks login.
/// </summary>
public sealed class FailoverOtpProvider(
    IServiceProvider services,
    IOtpSpendAlarm spendAlarm,
    ILogger<FailoverOtpProvider> logger,
    IReadOnlyList<string> providerOrder
) : IOtpProvider
{
    public async Task<OtpSendResult> SendAsync(
        string destination, string code, OtpPurpose purpose, OtpChannel channel,
        CancellationToken cancellationToken = default)
    {
        OtpSendResult? lastResult = null;

        foreach (var providerKey in providerOrder)
        {
            var provider = services.GetKeyedService<IOtpProvider>(providerKey);
            if (provider is null)
            {
                logger.LogWarning("[FailoverOtp] No provider registered for key {Key} — skipping.", providerKey);
                continue;
            }

            OtpSendResult result;
            try
            {
                result = await provider.SendAsync(destination, code, purpose, channel, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[FailoverOtp] Provider {Key} threw — trying next.", providerKey);
                result = new OtpSendResult(false, ex.Message);
            }

            await spendAlarm.RecordAttemptAsync(providerKey, channel, result.Succeeded, cancellationToken);
            if (result.Succeeded)
                return result;

            lastResult = result;
            logger.LogWarning("[FailoverOtp] Provider {Key} failed — failing over.", providerKey);
        }

        await spendAlarm.RecordExhaustedAsync(channel, cancellationToken);
        return lastResult ?? new OtpSendResult(false, "No OTP providers are configured.");
    }

    public async Task<OtpVerifyResult> VerifyAsync(
        string destination, string code, OtpPurpose purpose, CancellationToken cancellationToken = default)
    {
        // Verification is DB-backed at the application layer for every provider we ship; delegate to
        // the first configured provider for the provider-side path (e.g. Twilio Verify).
        foreach (var providerKey in providerOrder)
        {
            var provider = services.GetKeyedService<IOtpProvider>(providerKey);
            if (provider is not null)
                return await provider.VerifyAsync(destination, code, purpose, cancellationToken);
        }
        return new OtpVerifyResult(false, "No OTP providers are configured.");
    }
}
