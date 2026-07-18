using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Shared.Kernel.Exceptions;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.SuspendAccount;

public sealed class SuspendAccountHandler(
    IIdentityRepository repository,
    ILogger<SuspendAccountHandler> logger
) : IRequestHandler<SuspendAccountCommand, Result<SuspendAccountResponse>>
{
    public async Task<Result<SuspendAccountResponse>> Handle(
        SuspendAccountCommand request,
        CancellationToken cancellationToken)
    {
        var account = await repository.FindAccountByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
            return Result.Failure<SuspendAccountResponse>(Error.NotFound("resource", "Account not found."));

        try
        {
            account.Suspend(request.Reason);
        }
        catch (DomainException ex)
        {
            return Result.Failure<SuspendAccountResponse>(Error.Conflict("domain", ex.Message));
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "[SuspendAccount] AccountId={AccountId} suspended. Reason={Reason}",
            account.Id, request.Reason);

        return Result.Success(new SuspendAccountResponse(
            AccountId: account.Id,
            Message:   "Account suspended. Citizen has been notified."));
    }
}
