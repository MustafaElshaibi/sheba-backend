using MediatR;

namespace Sheba.Citizen.Application.Queries.GetMyProfile;

/// <summary>Fetches the authenticated citizen's own profile. AccountId comes from the token.</summary>
public sealed record GetMyProfileQuery(Guid AccountId) : IRequest<CitizenProfileResponse>;

public sealed record CitizenProfileResponse(
    Guid ProfileId,
    Guid AccountId,
    string MaskedNationalId,
    string FullNameAr,
    string FullNameEn,
    string? Email,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    string? Address,
    string? City,
    string? Governorate);
