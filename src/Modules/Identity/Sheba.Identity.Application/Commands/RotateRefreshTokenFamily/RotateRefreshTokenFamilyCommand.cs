using MediatR;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.RotateRefreshTokenFamily;

/// <summary>
/// Validates + rotates (or, on reuse, revokes) a refresh-token family (T-SEC-9, RFC 9700). Called
/// from the refresh_token grant with the family_id/family_generation claims OpenIddict restored
/// onto the principal from the presented token. PresentedGeneration matching the family's current
/// generation is a legitimate refresh (advance it); not matching means a superseded token was
/// replayed — the whole family gets revoked, not just this one request rejected.
/// </summary>
public sealed record RotateRefreshTokenFamilyCommand(
    Guid FamilyId,
    int PresentedGeneration
) : IRequest<Result<int>>;
