using MediatR;
using Sheba.Shared.Kernel.Exceptions;
using Sheba.Wallet.Domain.Interfaces;

namespace Sheba.Wallet.Application.Queries.GetRevocationStatus;

public sealed class GetRevocationStatusHandler(IWalletRepository repository)
    : IRequestHandler<GetRevocationStatusQuery, RevocationStatusDto>
{
    public async Task<RevocationStatusDto> Handle(GetRevocationStatusQuery query, CancellationToken ct)
    {
        var credential = await repository.GetCredentialByIdAsync(query.CredentialId, ct)
            ?? throw new NotFoundException("VerifiableCredential", query.CredentialId);

        return new RevocationStatusDto(credential.Id, credential.IsRevoked, credential.RevokedAt);
    }
}
