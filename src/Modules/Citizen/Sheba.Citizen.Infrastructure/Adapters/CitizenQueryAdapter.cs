using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sheba.Citizen.Infrastructure.Persistence;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Citizen.Infrastructure.Adapters;

/// <summary>
/// Cross-module adapter: implements ICitizenAccountQueryService from Shared.Kernel.
/// Other modules (ServiceRequest, Admin, Wallet) inject this to look up citizen
/// name/NID without depending on CitizenDbContext.
/// </summary>
public sealed class CitizenQueryAdapter(
    CitizenDbContext db,
    ILogger<CitizenQueryAdapter> logger) : ICitizenAccountQueryService
{
    public async Task<CitizenAccountInfo?> GetAccountInfoAsync(Guid accountId, CancellationToken ct = default)
    {
        var profile = await db.CitizenProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.AccountId == accountId, ct);

        if (profile is null)
        {
            logger.LogDebug("[CitizenQuery] No profile found for account {AccountId}", accountId);
            return null;
        }

        return new CitizenAccountInfo(
            AccountId: profile.AccountId,
            NationalId: profile.NationalId,
            FullNameAr: profile.FullNameAr,
            FullNameEn: profile.FullNameEn,
            IdentityLevel: 2, // LoA 2 — identity-verified citizen
            Email: profile.Email ?? string.Empty);
    }
}
