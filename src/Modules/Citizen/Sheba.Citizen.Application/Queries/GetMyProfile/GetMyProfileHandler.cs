using MediatR;
using Sheba.Citizen.Application.Interfaces;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Citizen.Application.Queries.GetMyProfile;

public sealed class GetMyProfileHandler(
    ICitizenProfileRepository profiles
) : IRequestHandler<GetMyProfileQuery, CitizenProfileResponse>
{
    public async Task<CitizenProfileResponse> Handle(GetMyProfileQuery query, CancellationToken ct)
    {
        var profile = await profiles.GetByAccountIdAsync(query.AccountId, ct)
            ?? throw new NotFoundException("CitizenProfile", query.AccountId.ToString());

        return new CitizenProfileResponse(
            ProfileId:        profile.Id,
            AccountId:        profile.AccountId,
            MaskedNationalId: MaskNid(profile.NationalId),
            FullNameAr:       profile.FullNameAr,
            FullNameEn:       profile.FullNameEn,
            Email:            profile.Email,
            PhoneNumber:      profile.PhoneNumber,
            DateOfBirth:      profile.DateOfBirth,
            Address:          profile.Address,
            City:             profile.City,
            Governorate:      profile.Governorate);
    }

    private static string MaskNid(string nid) =>
        nid.Length > 4
            ? string.Concat(new string('*', nid.Length - 4), nid[^4..])
            : "****";
}
