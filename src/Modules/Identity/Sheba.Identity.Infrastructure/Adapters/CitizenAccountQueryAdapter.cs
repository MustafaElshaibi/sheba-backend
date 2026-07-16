using Microsoft.EntityFrameworkCore;
using Sheba.Identity.Infrastructure.Persistence;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Infrastructure.Adapters;

/// <summary>
/// Implements ICitizenAccountQueryService using IdentityDbContext.
/// Lives in Identity.Infrastructure (which owns the accounts table).
/// Registered in IdentityModule so any module that injects
/// ICitizenAccountQueryService gets account data without touching the DbContext.
/// </summary>
public sealed class CitizenAccountQueryAdapter(IdentityDbContext db) : ICitizenAccountQueryService
{
    public async Task<CitizenAccountInfo?> GetAccountInfoAsync(Guid accountId, CancellationToken ct = default)
    {
        var account = await db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, ct);

        if (account is null)
            return null;

        return new CitizenAccountInfo(
            AccountId:     account.Id,
            NationalId:    account.NationalId,
            FullNameAr:    account.FullNameAr,
            FullNameEn:    account.FullNameEn,
            IdentityLevel: account.IdentityLevel,
            Email:         account.Email);
    }
}
