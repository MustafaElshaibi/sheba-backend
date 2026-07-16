using Microsoft.EntityFrameworkCore;
using Sheba.Citizen.Application.Interfaces;
using Sheba.Citizen.Domain.Entities;
using Sheba.Citizen.Infrastructure.Persistence;

namespace Sheba.Citizen.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of ICitizenProfileRepository.
/// </summary>
public sealed class CitizenProfileRepository(CitizenDbContext db) : ICitizenProfileRepository
{
    public async Task<CitizenProfile?> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
    {
        return await db.CitizenProfiles
            .FirstOrDefaultAsync(p => p.AccountId == accountId, ct);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await db.SaveChangesAsync(ct);
    }
}
