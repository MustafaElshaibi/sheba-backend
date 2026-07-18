using Sheba.Identity.Domain.Enums;

namespace Sheba.Identity.Domain.Interfaces;

/// <summary>
/// Observability hook for OTP delivery (T-INT-2). Fired on every send attempt so an operator can
/// wire alerting on cost/volume spikes (SMS is billed per message) and on failover/exhaustion.
/// Delivery must never fail because an alarm sink throws — implementations swallow their own errors.
/// </summary>
public interface IOtpSpendAlarm
{
    /// <summary>One provider's send attempt finished (success or failure).</summary>
    Task RecordAttemptAsync(string providerName, OtpChannel channel, bool succeeded, CancellationToken ct = default);

    /// <summary>Every provider in the failover chain failed — no OTP was delivered.</summary>
    Task RecordExhaustedAsync(OtpChannel channel, CancellationToken ct = default);
}
