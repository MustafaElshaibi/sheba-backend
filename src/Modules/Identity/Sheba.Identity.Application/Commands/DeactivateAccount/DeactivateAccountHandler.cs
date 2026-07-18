using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Shared.Kernel.Exceptions;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.DeactivateAccount;

public sealed class DeactivateAccountHandler(
    IIdentityRepository repository,
    ILogger<DeactivateAccountHandler> logger
) : IRequestHandler<DeactivateAccountCommand, Result<DeactivateAccountResponse>>
{
    public async Task<Result<DeactivateAccountResponse>> Handle(
        DeactivateAccountCommand request,
        CancellationToken cancellationToken)
    {
        var account = await repository.FindAccountByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
            return Result.Failure<DeactivateAccountResponse>(Error.NotFound("resource", "Account not found."));

        try
        {
            account.Deactivate(request.Reason);
        }
        catch (DomainException ex)
        {
            return Result.Failure<DeactivateAccountResponse>(Error.Conflict("domain", ex.Message));
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "[DeactivateAccount] AccountId={AccountId} deactivated. Reason={Reason}",
            account.Id, request.Reason);

        return Result.Success(new DeactivateAccountResponse(
            AccountId: account.Id,
            Message:   "Account deactivated. Citizen has been notified."));
    }
}
