using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.RequestLoaUpgrade;

/// <summary>
/// Opens a Level-of-Assurance upgrade request:
/// 1. Load account; must be Approved (only active accounts can upgrade)
/// 2. Target must be a real step up (2 or 3, and higher than current level)
/// 3. Guard: no other pending/under-review request already open
/// 4. Create IdentityRequest (UpgradeLoa2/3), mark under review → notifies admins
/// </summary>
public sealed class RequestLoaUpgradeHandler(
    IIdentityRepository repository,
    ILogger<RequestLoaUpgradeHandler> logger
) : IRequestHandler<RequestLoaUpgradeCommand, Result<RequestLoaUpgradeResponse>>
{
    public async Task<Result<RequestLoaUpgradeResponse>> Handle(
        RequestLoaUpgradeCommand request,
        CancellationToken cancellationToken)
    {
        if (request.TargetLevel is not (2 or 3))
            return Result.Failure<RequestLoaUpgradeResponse>(Error.Validation("target_level", "Target LoA level must be 2 or 3."));

        var account = await repository.FindAccountByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
            return Result.Failure<RequestLoaUpgradeResponse>(Error.NotFound("resource", "Account not found."));

        if (account.Status != AccountStatus.Approved)
        {
            return Result.Failure<RequestLoaUpgradeResponse>(Error.Conflict(
                "domain", "Only an active (approved) account can request an LoA upgrade."));
        }

        if (request.TargetLevel <= account.IdentityLevel)
        {
            return Result.Failure<RequestLoaUpgradeResponse>(Error.Conflict(
                "domain", $"Account is already at LoA {account.IdentityLevel} or higher."));
        }

        var existing = await repository.GetRequestsByAccountAsync(request.AccountId, cancellationToken);
        if (existing.Exists(r => r.Status is RequestStatus.Pending or RequestStatus.UnderReview))
        {
            return Result.Failure<RequestLoaUpgradeResponse>(Error.Conflict(
                "domain", "There is already a pending request for this account."));
        }

        var requestType = request.TargetLevel == 2 ? RequestType.UpgradeLoa2 : RequestType.UpgradeLoa3;

        var snapshot = new
        {
            account.NationalId,
            account.FullNameEn,
            CurrentLevel = account.IdentityLevel,
            request.TargetLevel,
            RequestedAt = DateTime.UtcNow
        };

        var identityRequest = IdentityRequest.Submit(request.AccountId, requestType, snapshot);
        await repository.AddIdentityRequestAsync(identityRequest, cancellationToken);
        identityRequest.MarkUnderReview();          // raises IdentityRequestSubmittedEvent
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "[RequestLoaUpgrade] AccountId={AccountId} requested LoA{Target}. RequestId={RequestId}",
            request.AccountId, request.TargetLevel, identityRequest.Id);

        return Result.Success(new RequestLoaUpgradeResponse(
            IdentityRequestId: identityRequest.Id,
            TargetLevel:       request.TargetLevel,
            Message:           $"Your request to upgrade to LoA {request.TargetLevel} has been submitted for review."));
    }
}
