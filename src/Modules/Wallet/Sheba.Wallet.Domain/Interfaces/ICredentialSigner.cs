namespace Sheba.Wallet.Domain.Interfaces;

/// <summary>Input claims for issuing a Digital Identity Credential.</summary>
public sealed record IdentityCredentialClaims(
    Guid SubjectId,
    string NationalIdMasked,
    string FullNameEn,
    string FullNameAr,
    int LevelOfAssurance);

/// <summary>Result of signing a W3C VC into a JWT.</summary>
public sealed record SignedCredential(
    string Jwt,
    string IssuerDid,
    string SubjectDid,
    string ClaimsJson,
    DateTime IssuedAt,
    DateTime ExpiresAt);

/// <summary>
/// Port for building and RSA-signing W3C Verifiable Credentials as JWTs.
/// Implemented in Infrastructure using System.Security.Cryptography (RSA) + JWT.
/// </summary>
public interface ICredentialSigner
{
    /// <summary>The issuer DID this signer represents (did:sheba:issuer).</summary>
    string IssuerDid { get; }

    /// <summary>The issuer's RSA public key in PEM (for the DID document).</summary>
    string IssuerPublicKeyPem { get; }

    /// <summary>Builds a W3C VC JWT for the given identity claims and signs it with RS256.</summary>
    SignedCredential SignIdentityCredential(IdentityCredentialClaims claims, TimeSpan validity);

    /// <summary>Verifies the RS256 signature of a VC-JWT against this issuer's key pair. Does not
    /// check expiry or revocation — callers combine this with the payload's exp claim and a
    /// repository lookup (T-WAL-2, verification/presentation flow, §5.6).</summary>
    bool VerifyIssuerSignature(string jwt);
}
