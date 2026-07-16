using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Citizen.Application.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Citizen.Application.Commands.UpdateProfile;

/// <summary>
/// Updates optional citizen profile fields.
/// Creates the profile if it doesn't exist yet (first update after approval).
/// </summary>
public sealed class UpdateProfileCommandHandler(
    ICitizenProfileRepository profiles,
    ILogger<UpdateProfileCommandHandler> logger
) : IRequestHandler<UpdateProfileCommand, UpdateProfileResponse>
{
    public async Task<UpdateProfileResponse> Handle(UpdateProfileCommand cmd, CancellationToken ct)
    {
        var profile = await profiles.GetByAccountIdAsync(cmd.AccountId, ct);

        if (profile is null)
            throw new NotFoundException("CitizenProfile", cmd.AccountId.ToString());

        profile.UpdateProfile(
            cmd.Email,
            cmd.PhoneNumber,
            cmd.DateOfBirth,
            cmd.Address,
            cmd.City,
            cmd.Governorate);

        await profiles.SaveChangesAsync(ct);

        logger.LogInformation("[UpdateProfile] Profile {ProfileId} updated for account {AccountId}",
            profile.Id, cmd.AccountId);

        return new UpdateProfileResponse(profile.Id, "Profile updated successfully.");
    }
}
