using MediatR;
using Microsoft.Extensions.Logging;
using Sheba.Wallet.Domain.Interfaces;

namespace Sheba.Wallet.Application.Commands.RevokeAccountCredentials;

/// <summary>Returns the number of credentials newly revoked.</summary>
public sealed class RevokeAccountCredentialsHandler(
    IWalletRepository repository,
    ILogger<RevokeAccountCredentialsHandler> logger
) : IRequestHandler<RevokeAccountCredentialsCommand, int>
{
    public async Task<int> Handle(RevokeAccountCredentialsCommand request, CancellationToken ct)
    {
        var credentials = await repository.GetCredentialsBySubjectAsync(request.AccountId, ct);
        var toRevoke = credentials.Where(c => !c.IsRevoked).ToList();
        if (toRevoke.Count == 0)
            return 0;

        foreach (var credential in toRevoke)
            credential.Revoke();

        await repository.SaveChangesAsync(ct);

        logger.LogInformation(
            "[RevokeAccountCredentials] Revoked {Count} credential(s) for AccountId={AccountId}",
            toRevoke.Count, request.AccountId);

        return toRevoke.Count;
    }
}
