using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.RejectIdentityRequest;

/// <summary>
/// Admin rejects an identity request:
/// 1. Load request + account
/// 2. Validate request is in Pending or UnderReview
/// 3. Call domain: request.Reject(adminId, reason, notes)
/// 4. Call domain: account.Reject()
/// 5. Persist — domain events raise IdentityRequestDecidedEvent (Approved=false)
/// </summary>
public sealed class RejectIdentityRequestHandler(
    IIdentityRepository repository,
    ILogger<RejectIdentityRequestHandler> logger
) : IRequestHandler<RejectIdentityRequestCommand, Result<RejectIdentityRequestResponse>>
{
    public async Task<Result<RejectIdentityRequestResponse>> Handle(
        RejectIdentityRequestCommand request,
        CancellationToken cancellationToken)
    {
        var identityRequest = await repository.FindRequestByIdAsync(request.RequestId, cancellationToken);
        if (identityRequest is null)
            return Result.Failure<RejectIdentityRequestResponse>(Error.NotFound("resource", "Identity request not found."));

        if (identityRequest.Status is not (RequestStatus.Pending or RequestStatus.UnderReview))
        {
            return Result.Failure<RejectIdentityRequestResponse>(Error.Conflict(
                "domain", $"Cannot reject request in status {identityRequest.Status}."));
        }

        var account = await repository.FindAccountByIdAsync(identityRequest.AccountId, cancellationToken);
        if (account is null)
            return Result.Failure<RejectIdentityRequestResponse>(Error.NotFound("resource", "Account not found."));

        identityRequest.Reject(request.ReviewedByAdminId, request.RejectionReason, request.Notes);
        account.Reject(request.RejectionReason);

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "[RejectIdentityRequest] RequestId={RequestId} rejected by AdminId={AdminId} Reason={Reason}",
            request.RequestId, request.ReviewedByAdminId, request.RejectionReason);

        return Result.Success(new RejectIdentityRequestResponse(
            RequestId: request.RequestId,
            AccountId: account.Id,
            Message:   "Identity request rejected. Citizen has been notified."));
    }
}
