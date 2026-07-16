namespace Sheba.Shared.Kernel.Interfaces;

/// <summary>
/// Cross-module query service for looking up citizen account info.
/// Defined in Shared.Kernel so any module can depend on it.
/// Implemented in Identity.Infrastructure (which owns the data).
///
/// This follows the architecture's prescribed pattern:
///   "ICitizenQueryService interface defined in Sheba.Shared.Kernel
///    → Citizen module provides the implementation
///    → Other modules inject ICitizenAccountQueryService (not DbContext)"
/// </summary>
public interface ICitizenAccountQueryService
{
    Task<CitizenAccountInfo?> GetAccountInfoAsync(Guid accountId, CancellationToken ct = default);
}

/// <summary>
/// Read-only DTO for cross-module citizen account queries.
/// Contains only the data other modules need — no passwords, no security counters.
/// </summary>
public sealed record CitizenAccountInfo(
    Guid AccountId,
    string NationalId,
    string FullNameAr,
    string FullNameEn,
    int IdentityLevel,
    string Email);
