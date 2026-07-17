using MediatR;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.CreateRefreshTokenFamily;

/// <summary>
/// Starts a new refresh-token family (T-SEC-9) at generation 0. Called from OidcEndpoints
/// whenever a token response actually grants offline_access — SubjectId is whichever principal
/// type is signing in (citizen AccountId or admin AdminId), never a caller-supplied value.
/// </summary>
public sealed record CreateRefreshTokenFamilyCommand(
    Guid SubjectId,
    string ClientId
) : IRequest<Result<CreateRefreshTokenFamilyResponse>>;

public sealed record CreateRefreshTokenFamilyResponse(Guid FamilyId, int Generation);
