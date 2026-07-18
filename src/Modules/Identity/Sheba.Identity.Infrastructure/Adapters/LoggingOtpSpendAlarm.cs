using Microsoft.Extensions.Logging;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Domain.Interfaces;

namespace Sheba.Identity.Infrastructure.Adapters;

/// <summary>
/// Default <see cref="IOtpSpendAlarm"/>: structured logs an operator can alert on (e.g. a Serilog
/// sink → alerting rule). A production deployment swaps this for a metrics/PagerDuty sink via config.
/// Never throws — OTP delivery must not fail because telemetry did.
/// </summary>
public sealed class LoggingOtpSpendAlarm(ILogger<LoggingOtpSpendAlarm> logger) : IOtpSpendAlarm
{
    public Task RecordAttemptAsync(string providerName, OtpChannel channel, bool succeeded, CancellationToken ct = default)
    {
        // No PII: only provider name, channel, and outcome are logged — never the destination or code.
        if (succeeded)
            logger.LogInformation("[OtpSpend] provider={Provider} channel={Channel} outcome=sent", providerName, channel);
        else
            logger.LogWarning("[OtpSpend] provider={Provider} channel={Channel} outcome=failed", providerName, channel);
        return Task.CompletedTask;
    }

    public Task RecordExhaustedAsync(OtpChannel channel, CancellationToken ct = default)
    {
        logger.LogError("[OtpSpend] ALARM channel={Channel} outcome=all-providers-exhausted — no OTP delivered", channel);
        return Task.CompletedTask;
    }
}
