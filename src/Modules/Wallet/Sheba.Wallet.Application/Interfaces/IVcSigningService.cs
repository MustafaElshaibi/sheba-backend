namespace Sheba.Wallet.Application.Interfaces;

/// <summary>
/// Port for signing JWT-based W3C Verifiable Credentials.
/// Implemented in Infrastructure using RSA keys.
/// </summary>
public interface IVcSigningService
{
    /// <summary>Builds and signs a JWT-VC containing the given claims. Returns the compact JWT string.</summary>
    string SignCredentialJwt(
        string issuerDid,
        string subjectDid,
        string credentialId,
        string credentialType,
        Dictionary<string, object> claims,
        DateTime issuedAt,
        DateTime expiresAt);

    /// <summary>Returns the issuer's public key as a JWK JSON string (for DID document creation).</summary>
    string GetPublicKeyJwk();
}
