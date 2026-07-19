using MediatR;
using Sheba.Shared.Kernel.Exceptions;
using Sheba.Wallet.Domain.Interfaces;

namespace Sheba.Wallet.Application.Queries.GetDidDocument;

public sealed class GetDidDocumentHandler(IWalletRepository repository)
    : IRequestHandler<GetDidDocumentQuery, DidDocumentDto>
{
    public async Task<DidDocumentDto> Handle(GetDidDocumentQuery query, CancellationToken ct)
    {
        var did = await repository.GetByDidAsync(query.Did, ct)
            ?? throw new NotFoundException("DidDocument", query.Did);

        return new DidDocumentDto(did.Did, did.PublicKeyPem, did.IsActive);
    }
}
