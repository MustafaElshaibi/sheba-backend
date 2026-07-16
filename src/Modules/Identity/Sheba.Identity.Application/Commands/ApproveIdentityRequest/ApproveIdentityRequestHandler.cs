using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Identity.Application.Commands.ApproveIdentityRequest;

/// <summary>
/// Admin approves an identity request:
/// 1. Load request + account
/// 2. Validate request is in UnderReview or Pending status
/// 3. Call domain method: request.Approve(adminId, notes)
/// 4. Call domain method: account.Approve()
/// 5. Persist — domain events raise IdentityRequestDecidedEvent
/// </summary>
public sealed class ApproveIdentityRequestHandler(
    IIdentityRepository repository,
    ILogger<ApproveIdentityRequestHandler> logger
) : IRequestHandler<ApproveIdentityRequestCommand, ApproveIdentityRequestResponse>
{
    public async Task<ApproveIdentityRequestResponse> Handle(
        ApproveIdentityRequestCommand request,
        CancellationToken cancellationToken)
    {
        var identityRequest = await repository.FindRequestByIdAsync(request.RequestId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.IdentityRequest), request.RequestId);

        if (identityRequest.Status is not (RequestStatus.Pending or RequestStatus.UnderReview))
        {
            throw new DomainException(
                $"Cannot approve request in status {identityRequest.Status}. Only Pending or UnderReview requests can be approved.");
        }

        var account = await repository.FindAccountByIdAsync(identityRequest.AccountId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Account), identityRequest.AccountId);

        // Domain methods raise domain events
        identityRequest.Approve(request.ReviewedByAdminId, request.Notes);

        // Apply the effect of the request to the account:
        //   • OpenAccount / ReopenAccount → activate the account
        //   • UpgradeLoa2 / UpgradeLoa3   → raise the account's Level of Assurance
        switch (identityRequest.RequestType)
        {
            case RequestType.UpgradeLoa2:
                account.UpgradeIdentityLevel(2);
                break;
            case RequestType.UpgradeLoa3:
                account.UpgradeIdentityLevel(3);
                break;
            default:
                account.Approve();
                break;
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "[ApproveIdentityRequest] RequestId={RequestId} approved by AdminId={AdminId}",
            request.RequestId, request.ReviewedByAdminId);

        return new ApproveIdentityRequestResponse(
            RequestId: request.RequestId,
            AccountId: account.Id,
            Message:   "Identity request approved. Citizen account is now active.");
    }
}
