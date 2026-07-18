using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Identity.Application.Interfaces;
using Sheba.Shared.Kernel.Exceptions;
using Sheba.Shared.Kernel.Results;

namespace Sheba.Identity.Application.Commands.ReinstateAccount;

public sealed class ReinstateAccountHandler(
    IIdentityRepository repository,
    ILogger<ReinstateAccountHandler> logger
) : IRequestHandler<ReinstateAccountCommand, Result<ReinstateAccountResponse>>
{
    public async Task<Result<ReinstateAccountResponse>> Handle(
        ReinstateAccountCommand request,
        CancellationToken cancellationToken)
    {
        var account = await repository.FindAccountByIdAsync(request.AccountId, cancellationToken);
        if (account is null)
            return Result.Failure<ReinstateAccountResponse>(Error.NotFound("resource", "Account not found."));

        try
        {
            account.Reinstate();
        }
        catch (DomainException ex)
        {
            return Result.Failure<ReinstateAccountResponse>(Error.Conflict("domain", ex.Message));
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("[ReinstateAccount] AccountId={AccountId} reinstated.", account.Id);

        return Result.Success(new ReinstateAccountResponse(
            AccountId: account.Id,
            Message:   "Account reinstated. Citizen has been notified."));
    }
}
