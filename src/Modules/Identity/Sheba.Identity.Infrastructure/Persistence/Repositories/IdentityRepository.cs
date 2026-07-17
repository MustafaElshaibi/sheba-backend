using Microsoft.EntityFrameworkCore;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Infrastructure.Persistence;

namespace Sheba.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IIdentityRepository.
/// Injected into Application handlers via DI — Application layer sees only the interface.
///
/// All methods are async; queries use AsNoTracking where the entity is read-only.
/// Writable entities are tracked so that SaveChangesAsync persists the changes.
///
/// Domain event dispatch: SaveChangesAsync is a plain passthrough to the DbContext.
/// The OutboxSaveChangesInterceptor (registered on IdentityDbContext, T-EVT-1) converts
/// raised domain events into outbox_messages rows in the same SaveChanges call, so events
/// commit atomically with the aggregate write. A separate Hangfire dispatcher publishes them.
/// </summary>
public sealed class IdentityRepository(IdentityDbContext db) : IIdentityRepository
{
    // ── Account ───────────────────────────────────────────────────────────────

    public async Task AddAccountAsync(Account account, CancellationToken ct = default)
        => await db.Accounts.AddAsync(account, ct);

    public async Task<Account?> FindAccountByNidAsync(string nationalId, CancellationToken ct = default)
        => await db.Accounts
                   .FirstOrDefaultAsync(a => a.NationalId == nationalId, ct);

    public async Task<Account?> FindAccountByIdAsync(Guid accountId, CancellationToken ct = default)
        => await db.Accounts
                   .FirstOrDefaultAsync(a => a.Id == accountId, ct);

    public async Task<Account?> FindAccountByUsernameOrNidAsync(string usernameOrNid, CancellationToken ct = default)
        => await db.Accounts
                   .FirstOrDefaultAsync(
                       a => a.Username == usernameOrNid || a.NationalId == usernameOrNid,
                       ct);

    // ── IdentityRequest ───────────────────────────────────────────────────────

    public async Task AddIdentityRequestAsync(IdentityRequest request, CancellationToken ct = default)
        => await db.IdentityRequests.AddAsync(request, ct);

    public async Task<IdentityRequest?> FindRequestByIdAsync(Guid requestId, CancellationToken ct = default)
        => await db.IdentityRequests
                   .FirstOrDefaultAsync(r => r.Id == requestId, ct);

    public async Task<List<IdentityRequest>> GetPendingRequestsAsync(
        int pageSize, int pageNumber, CancellationToken ct = default)
    {
        return await db.IdentityRequests
                       .Where(r => r.Status == RequestStatus.Pending
                                || r.Status == RequestStatus.UnderReview)
                       .OrderByDescending(r => r.SubmittedAt)
                       .Skip((pageNumber - 1) * pageSize)
                       .Take(pageSize)
                       .ToListAsync(ct);
    }

    public async Task<List<IdentityRequest>> GetRequestsByAccountAsync(
        Guid accountId, CancellationToken ct = default)
    {
        return await db.IdentityRequests
                       .Where(r => r.AccountId == accountId)
                       .OrderByDescending(r => r.SubmittedAt)
                       .ToListAsync(ct);
    }

    // ── OtpRecord ─────────────────────────────────────────────────────────────

    public async Task AddOtpRecordAsync(OtpRecord record, CancellationToken ct = default)
        => await db.OtpRecords.AddAsync(record, ct);

    public async Task<OtpRecord?> FindActiveOtpAsync(
        Guid accountId, OtpPurpose purpose, CancellationToken ct = default)
    {
        return await db.OtpRecords
                       .Where(o => o.AccountId == accountId
                                && o.Purpose    == purpose
                                && o.UsedAt     == null
                                && o.ExpiresAt  > DateTime.UtcNow)
                       .OrderByDescending(o => o.CreatedAt)
                       .FirstOrDefaultAsync(ct);
    }

    public async Task InvalidatePreviousOtpsAsync(
        Guid accountId, OtpPurpose purpose, CancellationToken ct = default)
    {
        // Bulk-mark all active OTPs for this account+purpose as expired
        var active = await db.OtpRecords
                             .Where(o => o.AccountId == accountId
                                      && o.Purpose    == purpose
                                      && o.UsedAt     == null
                                      && o.ExpiresAt  > DateTime.UtcNow)
                             .ToListAsync(ct);

        foreach (var otp in active)
            otp.Expire();   // sets ExpiresAt = DateTime.UtcNow - 1s via domain method
    }

    // ── Unit of Work ─────────────────────────────────────────────────────────

    // ── AdminUser ────────────────────────────────────────────────────────────
    public async Task AddAdminUserAsync(AdminUser admin, CancellationToken ct = default)
        => await db.AdminUsers.AddAsync(admin, ct);

    public async Task<AdminUser?> FindAdminByEmployeeIdAsync(string employeeId, CancellationToken ct = default)
        => await db.AdminUsers.FirstOrDefaultAsync(a => a.EmployeeId == employeeId, ct);

    public async Task<AdminUser?> FindAdminByEmailAsync(string email, CancellationToken ct = default)
        => await db.AdminUsers.FirstOrDefaultAsync(a => a.Email == email, ct);

    public async Task<AdminUser?> FindAdminByIdAsync(Guid adminId, CancellationToken ct = default)
        => await db.AdminUsers.FirstOrDefaultAsync(a => a.Id == adminId, ct);

    // ── AdminRecoveryCode ────────────────────────────────────────────────────
    public async Task AddAdminRecoveryCodesAsync(IEnumerable<AdminRecoveryCode> codes, CancellationToken ct = default)
        => await db.AdminRecoveryCodes.AddRangeAsync(codes, ct);

    public async Task<List<AdminRecoveryCode>> GetUnusedAdminRecoveryCodesAsync(
        Guid adminUserId, CancellationToken ct = default)
    {
        return await db.AdminRecoveryCodes
                       .Where(c => c.AdminUserId == adminUserId && c.UsedAt == null)
                       .ToListAsync(ct);
    }

    // ── Unit of Work ─────────────────────────────────────────────────────────
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
