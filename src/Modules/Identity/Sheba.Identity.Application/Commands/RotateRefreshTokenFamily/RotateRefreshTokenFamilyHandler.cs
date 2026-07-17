using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.RotateRefreshTokenFamily;

public sealed class RotateRefreshTokenFamilyHandler(
    IIdentityRepository repository,
    ILogger<RotateRefreshTokenFamilyHandler> logger
) : IRequestHandler<RotateRefreshTokenFamilyCommand, Result<int>>
{
    private const string RevokedMessage = "This session has been revoked. Please sign in again.";

    public async Task<Result<int>> Handle(RotateRefreshTokenFamilyCommand request, CancellationToken ct)
    {
        var family = await repository.FindRefreshTokenFamilyByFamilyIdAsync(request.FamilyId, ct);
        if (family is null)
        {
            // No record for this family_id — a pre-feature token, or one issued without
            // offline_access ever going through family tracking. Nothing to check; defer
            // entirely to OpenIddict's own token validation, which already ran successfully
            // before this handler is reached.
            return Result.Success(request.PresentedGeneration);
        }

        if (family.IsRevoked)
        {
            logger.LogWarning(
                "[RotateRefreshTokenFamily] Refresh attempted on revoked FamilyId={FamilyId}", request.FamilyId);
            return Result.Failure<int>(Error.Unauthorized("refresh_token", RevokedMessage));
        }

        if (request.PresentedGeneration != family.Generation)
        {
            family.Revoke("Refresh-token reuse detected: stale generation");
            await repository.SaveChangesAsync(ct);
            logger.LogWarning(
                "[RotateRefreshTokenFamily] Reuse detected for FamilyId={FamilyId}: presented " +
                "generation {Presented}, current was {Current}. Family revoked.",
                request.FamilyId, request.PresentedGeneration, family.Generation);
            return Result.Failure<int>(Error.Unauthorized("refresh_token", RevokedMessage));
        }

        family.Rotate();
        await repository.SaveChangesAsync(ct);

        return Result.Success(family.Generation);
    }
}
