using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;

namespace Sheba.Identity.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job (T-ID-1): frees up abandoned registrations and sweeps spent OTP
/// records.
///
/// Registered in Program.cs:
///   RecurringJob.AddOrUpdate&lt;AccountPurgeJob&gt;(
///       "account-purge", job => job.PurgeAsync(CancellationToken.None), Cron.Hourly());
///
/// 1. Accounts still PendingVerification `Identity:PendingVerificationPurgeHours` (default 24h)
///    after their last update never completed OTP verification — they are hard-deleted (along
///    with their OtpRecord and IdentityRequest rows) so the national ID is free to register
///    again. A citizen who genuinely abandoned registration is not permanently blocked from
///    trying again. Cutoff is on UpdatedAt, not CreatedAt, so a Rejected account that just
///    re-applied (Account.ReApply reuses the row) is not immediately purge-eligible.
/// 2. OTP records that are already used or expired more than an hour ago are hard-deleted
///    everywhere (OtpRecord's own doc comment has always said "purged on use or expiry" — this
///    job is what finally does it).
/// </summary>
public sealed class AccountPurgeJob(
    IIdentityRepository repository,
    IConfiguration configuration,
    ILogger<AccountPurgeJob> logger)
{
    public async Task PurgeAsync(CancellationToken ct)
    {
        var pendingVerificationHours = configuration.GetValue("Identity:PendingVerificationPurgeHours", 24);
        var pendingCutoff = DateTime.UtcNow.AddHours(-pendingVerificationHours);

        var expiredAccounts = await repository.GetExpiredPendingVerificationAccountsAsync(pendingCutoff, ct);
        foreach (var account in expiredAccounts)
        {
            logger.LogInformation(
                "[AccountPurge] Purging abandoned PendingVerification account {AccountId} (created {CreatedAt})",
                account.Id, account.CreatedAt);
            await repository.PurgeAccountAsync(account.Id, ct);
        }

        var otpCutoff = DateTime.UtcNow.AddHours(-1);
        var purgedOtps = await repository.PurgeSpentOtpRecordsAsync(otpCutoff, ct);

        await repository.SaveChangesAsync(ct);

        logger.LogInformation(
            "[AccountPurge] Purged {AccountCount} abandoned account(s), {OtpCount} spent OTP record(s)",
            expiredAccounts.Count, purgedOtps);
    }
}
