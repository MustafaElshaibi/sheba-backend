using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Wallet.Domain.Entities;

/// <summary>
/// A W3C Verifiable Credential issued to a citizen, stored as a signed JWT.
/// The JWT (compact JWS) contains the verifiable claims and is signed by Sheba's RSA key.
/// </summary>
public sealed class VerifiableCredential : BaseEntity
{
    public Guid SubjectId { get; private set; }              // citizen/account the VC is about
    public string CredentialType { get; private set; } = string.Empty;   // e.g. "DigitalIdentityCredential"
    public string IssuerDid { get; private set; } = string.Empty;        // did:sheba:...
    public string SubjectDid { get; private set; } = string.Empty;       // did:sheba:citizen:...
    public string Jwt { get; private set; } = string.Empty;              // the signed JWS (compact)
    public string ClaimsJson { get; private set; } = "{}";               // decoded claims for quick reads
    public DateTime IssuedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    private VerifiableCredential() { }

    public static VerifiableCredential Issue(
        Guid subjectId,
        string credentialType,
        string issuerDid,
        string subjectDid,
        string jwt,
        string claimsJson,
        DateTime? expiresAt = null)
    {
        if (subjectId == Guid.Empty) throw new DomainException("Subject ID is required.");
        if (string.IsNullOrWhiteSpace(jwt)) throw new DomainException("VC JWT is required.");

        return new VerifiableCredential
        {
            SubjectId = subjectId,
            CredentialType = credentialType,
            IssuerDid = issuerDid,
            SubjectDid = subjectDid,
            Jwt = jwt,
            ClaimsJson = claimsJson,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };
    }

    public void Revoke()
    {
        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
        Touch();
    }
}
