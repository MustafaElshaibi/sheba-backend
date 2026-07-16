using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Wallet.Domain.Entities;

/// <summary>
/// A Decentralized Identifier (DID) document. For the graduation build, Sheba acts as the
/// single issuer DID (did:sheba:issuer) and each citizen gets a subject DID (did:sheba:citizen:{id}).
/// </summary>
public sealed class DidDocument : BaseEntity
{
    public string Did { get; private set; } = string.Empty;          // did:sheba:...
    public Guid? SubjectId { get; private set; }                     // null for the issuer DID
    public string PublicKeyPem { get; private set; } = string.Empty; // RSA public key (PEM)
    public bool IsActive { get; private set; } = true;

    private DidDocument() { }

    public static DidDocument Create(string did, string publicKeyPem, Guid? subjectId = null)
    {
        if (string.IsNullOrWhiteSpace(did)) throw new DomainException("DID is required.");

        return new DidDocument
        {
            Did = did,
            SubjectId = subjectId,
            PublicKeyPem = publicKeyPem
        };
    }
}
