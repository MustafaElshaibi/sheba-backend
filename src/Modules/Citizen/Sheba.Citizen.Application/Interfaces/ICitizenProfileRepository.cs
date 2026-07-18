using Sheba.Citizen.Domain.Entities;

namespace Sheba.Citizen.Application.Interfaces;

/// <summary>
/// Application-layer repository abstraction for the Citizen module.
/// Implemented by EF Core in Sheba.Citizen.Infrastructure.
/// </summary>
public interface ICitizenProfileRepository
{
    /// <summary>Loads a tracked profile by its owning account id, or null if none exists.</summary>
    Task<CitizenProfile?> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default);

    Task AddAsync(CitizenProfile profile, CancellationToken ct = default);

    /// <summary>Persists pending changes (Unit of Work).</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
