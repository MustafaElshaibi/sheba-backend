using Sheba.Identity.Domain.Entities;

namespace Sheba.Identity.Application.Interfaces;

public interface IIdentityRepository
{
    // ── Account ──────────────────────────────────────────────────────────────
    Task AddAccountAsync(Account account, CancellationToken ct = default);
    Task<Account?> FindAccountByNidAsync(string nationalId, CancellationToken ct = default);
    Task<Account?> FindAccountByIdAsync(Guid accountId, CancellationToken ct = default);
    Task<Account?> FindAccountByUsernameOrNidAsync(string usernameOrNid, CancellationToken ct = default);

    // ── IdentityRequest ───────────────────────────────────────────────────────
    Task AddIdentityRequestAsync(IdentityRequest request, CancellationToken ct = default);
    Task<IdentityRequest?> FindRequestByIdAsync(Guid requestId, CancellationToken ct = default);
    Task<List<IdentityRequest>> GetPendingRequestsAsync(int pageSize, int pageNumber, CancellationToken ct = default);
    Task<List<IdentityRequest>> GetRequestsByAccountAsync(Guid accountId, CancellationToken ct = default);

    // ── OtpRecord ─────────────────────────────────────────────────────────────
    Task AddOtpRecordAsync(OtpRecord record, CancellationToken ct = default);
    Task<OtpRecord?> FindActiveOtpAsync(Guid accountId, Domain.Enums.OtpPurpose purpose, CancellationToken ct = default);
    Task InvalidatePreviousOtpsAsync(Guid accountId, Domain.Enums.OtpPurpose purpose, CancellationToken ct = default);

    // ── Purge (T-ID-1) ───────────────────────────────────────────────────────
    /// <summary>Abandoned registrations: still PendingVerification past the given cutoff.</summary>
    Task<List<Account>> GetExpiredPendingVerificationAccountsAsync(DateTime olderThan, CancellationToken ct = default);

    /// <summary>Hard-deletes an account and its OtpRecord/IdentityRequest rows (frees the NID).</summary>
    Task PurgeAccountAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>Hard-deletes used-up or long-expired OTP records across all accounts. Returns the count removed.</summary>
    Task<int> PurgeSpentOtpRecordsAsync(DateTime expiredBefore, CancellationToken ct = default);

    // ── AdminUser ──────────────────────────────────────────────────────────────
    Task AddAdminUserAsync(AdminUser admin, CancellationToken ct = default);
    Task<AdminUser?> FindAdminByEmployeeIdAsync(string employeeId, CancellationToken ct = default);
    Task<AdminUser?> FindAdminByEmailAsync(string email, CancellationToken ct = default);
    Task<AdminUser?> FindAdminByIdAsync(Guid adminId, CancellationToken ct = default);

    // ── AdminRecoveryCode ────────────────────────────────────────────────────
    Task AddAdminRecoveryCodesAsync(IEnumerable<AdminRecoveryCode> codes, CancellationToken ct = default);
    Task<List<AdminRecoveryCode>> GetUnusedAdminRecoveryCodesAsync(Guid adminUserId, CancellationToken ct = default);

    // ── RefreshTokenFamily ───────────────────────────────────────────────────
    Task AddRefreshTokenFamilyAsync(RefreshTokenFamily family, CancellationToken ct = default);
    Task<RefreshTokenFamily?> FindRefreshTokenFamilyByFamilyIdAsync(Guid familyId, CancellationToken ct = default);

    // ── Unit of Work ─────────────────────────────────────────────────────────
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
