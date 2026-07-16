using Sheba.Shared.Kernel.Entities;

namespace Sheba.Citizen.Domain.Entities;

/// <summary>
/// Extended citizen profile data.
/// The Account entity lives in the Identity module — CitizenProfile extends it
/// with optional personal info that the citizen can update after approval.
///
/// AccountId is a 1:1 FK to identity.accounts but NOT an EF navigation
/// (cross-schema references are by convention, not by FK constraint).
/// </summary>
public sealed class CitizenProfile : BaseEntity
{
    /// <summary>Maps to the Account.Id in the identity schema.</summary>
    public Guid AccountId { get; private set; }

    public string NationalId { get; private set; } = default!;
    public string FullNameAr { get; private set; } = default!;
    public string FullNameEn { get; private set; } = default!;
    public string? Email { get; private set; }
    public string? PhoneNumber { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public string? Address { get; private set; }
    public string? City { get; private set; }
    public string? Governorate { get; private set; }

    private CitizenProfile() { }

    /// <summary>Creates a new citizen profile, typically after identity approval.</summary>
    public static CitizenProfile Create(
        Guid accountId,
        string nationalId,
        string fullNameAr,
        string fullNameEn,
        string? email = null,
        string? phoneNumber = null)
    {
        return new CitizenProfile
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            NationalId = nationalId,
            FullNameAr = fullNameAr,
            FullNameEn = fullNameEn,
            Email = email,
            PhoneNumber = phoneNumber
        };
    }

    /// <summary>Updates optional profile fields.</summary>
    public void UpdateProfile(
        string? email,
        string? phoneNumber,
        DateOnly? dateOfBirth,
        string? address,
        string? city,
        string? governorate)
    {
        Email = email ?? Email;
        PhoneNumber = phoneNumber ?? PhoneNumber;
        DateOfBirth = dateOfBirth ?? DateOfBirth;
        Address = address ?? Address;
        City = city ?? City;
        Governorate = governorate ?? Governorate;
        Touch();
    }
}
