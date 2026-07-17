using MediatR;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.CreateRefreshTokenFamily;

public sealed class CreateRefreshTokenFamilyHandler(
    IIdentityRepository repository
) : IRequestHandler<CreateRefreshTokenFamilyCommand, Result<CreateRefreshTokenFamilyResponse>>
{
    // Matches IdentityModule.cs's server.SetRefreshTokenLifetime(TimeSpan.FromDays(30)) — the two
    // aren't derived from a shared constant today (neither is config-driven), so keep them in sync
    // by hand if either changes.
    private static readonly TimeSpan FamilyLifetime = TimeSpan.FromDays(30);

    public async Task<Result<CreateRefreshTokenFamilyResponse>> Handle(
        CreateRefreshTokenFamilyCommand request, CancellationToken ct)
    {
        var family = RefreshTokenFamily.Create(
            request.SubjectId,
            request.ClientId,
            familyId: Guid.NewGuid(),
            expiresAt: DateTime.UtcNow.Add(FamilyLifetime));

        await repository.AddRefreshTokenFamilyAsync(family, ct);
        await repository.SaveChangesAsync(ct);

        return Result.Success(new CreateRefreshTokenFamilyResponse(family.FamilyId, family.Generation));
    }
}
