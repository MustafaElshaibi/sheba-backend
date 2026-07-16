using MediatR;

namespace Sheba.Citizen.Application.Commands.UpdateProfile;

/// <summary>
/// Updates the authenticated citizen's optional profile fields.
/// </summary>
public sealed record UpdateProfileCommand(
    Guid AccountId,
    string? Email,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    string? Address,
    string? City,
    string? Governorate
) : IRequest<UpdateProfileResponse>;

public sealed record UpdateProfileResponse(
    Guid ProfileId,
    string Message);
