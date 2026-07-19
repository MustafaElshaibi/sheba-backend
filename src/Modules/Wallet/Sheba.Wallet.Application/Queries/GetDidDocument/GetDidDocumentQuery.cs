using MediatR;

namespace Sheba.Wallet.Application.Queries.GetDidDocument;

/// <summary>
/// Public DID resolution (T-WAL-2, BR-WA-2 "verifies against Sheba's published keys") — a
/// relying party independently verifying a VC-JWT's signature needs the issuer's public key,
/// resolvable by DID the same way the JWT's own `iss`/`kid` claims name it.
/// </summary>
public sealed record GetDidDocumentQuery(string Did) : IRequest<DidDocumentDto>;

public sealed record DidDocumentDto(string Did, string PublicKeyPem, bool IsActive);
