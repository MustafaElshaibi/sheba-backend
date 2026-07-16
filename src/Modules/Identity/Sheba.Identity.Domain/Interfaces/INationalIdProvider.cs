namespace Sheba.Identity.Domain.Interfaces;

/// <summary>
/// Result returned by the civil registry (NID) lookup.
/// </summary>
public sealed record NationalIdLookupResult(
    bool IsFound,
    string NationalId,
    string FullNameAr,
    string FullNameEn,
    string PhoneNumber,
    DateOnly DateOfBirth,
    string Gender,
    NidStatus Status
);

public enum NidStatus
{
    Valid,
    Deceased,
    Suspended,
    Expired,
    NotFound
}

/// <summary>
/// Port (interface) for the civil registry / national ID validation adapter.
/// Development: MockNationalIdProvider  |  Production: HttpNationalIdProvider
/// Switched via configuration key NationalId:ActiveProvider.
/// </summary>
public interface INationalIdProvider
{
    /// <summary>
    /// Looks up a citizen in the civil registry by NID + phone number.
    /// Returns a result object — never throws on "not found".
    /// Throws only on infrastructure failures (network, timeout).
    /// </summary>
    Task<NationalIdLookupResult> LookupAsync(
        string nationalId,
        string phoneNumber,
        CancellationToken cancellationToken = default);
}
