using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Sheba.Wallet.Domain.Interfaces;

namespace Sheba.Wallet.Infrastructure.Credentials;

/// <summary>
/// Builds W3C Verifiable Credentials as RS256-signed JWTs using System.Security.Cryptography.
///
/// The RSA private key is read from configuration (Wallet:IssuerPrivateKeyPem). If absent,
/// a key is generated at startup (dev only — non-persistent across restarts).
///
/// JWT structure (VC-JWT per W3C VC Data Model 1.1):
///   header:  { alg: RS256, typ: JWT, kid: {issuerDid}#key-1 }
///   payload: { iss, sub, nbf, exp, jti, vc: { @context, type, credentialSubject } }
/// </summary>
public sealed class RsaCredentialSigner : ICredentialSigner
{
    private readonly RSA _rsa;
    public string IssuerDid { get; }
    public string IssuerPublicKeyPem { get; }

    public RsaCredentialSigner(IConfiguration configuration)
    {
        IssuerDid = configuration["Wallet:IssuerDid"] ?? "did:sheba:issuer";

        _rsa = RSA.Create(2048);
        var privatePem = configuration["Wallet:IssuerPrivateKeyPem"];
        if (!string.IsNullOrWhiteSpace(privatePem))
            _rsa.ImportFromPem(privatePem);

        IssuerPublicKeyPem = _rsa.ExportSubjectPublicKeyInfoPem();
    }

    public SignedCredential SignIdentityCredential(IdentityCredentialClaims claims, TimeSpan validity)
    {
        var now = DateTimeOffset.UtcNow;
        var exp = now.Add(validity);
        var subjectDid = $"did:sheba:citizen:{claims.SubjectId}";
        var jti = $"urn:uuid:{Guid.NewGuid()}";

        // ── W3C VC credentialSubject ──────────────────────────────────────────
        var credentialSubject = new Dictionary<string, object>
        {
            ["id"] = subjectDid,
            ["nationalId"] = claims.NationalIdMasked,
            ["fullNameEn"] = claims.FullNameEn,
            ["fullNameAr"] = claims.FullNameAr,
            ["levelOfAssurance"] = claims.LevelOfAssurance
        };

        var vc = new Dictionary<string, object>
        {
            ["@context"] = new[] { "https://www.w3.org/2018/credentials/v1" },
            ["type"] = new[] { "VerifiableCredential", "DigitalIdentityCredential" },
            ["issuer"] = IssuerDid,
            ["issuanceDate"] = now.UtcDateTime.ToString("O"),
            ["credentialSubject"] = credentialSubject
        };

        var payload = new Dictionary<string, object>
        {
            ["iss"] = IssuerDid,
            ["sub"] = subjectDid,
            ["nbf"] = now.ToUnixTimeSeconds(),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = exp.ToUnixTimeSeconds(),
            ["jti"] = jti,
            ["vc"] = vc
        };

        var header = new Dictionary<string, object>
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT",
            ["kid"] = $"{IssuerDid}#key-1"
        };

        var jwt = BuildSignedJwt(header, payload);
        var claimsJson = JsonSerializer.Serialize(credentialSubject);

        return new SignedCredential(jwt, IssuerDid, subjectDid, claimsJson, now.UtcDateTime, exp.UtcDateTime);
    }

    private string BuildSignedJwt(
        IReadOnlyDictionary<string, object> header,
        IReadOnlyDictionary<string, object> payload)
    {
        var headerB64 = Base64Url(JsonSerializer.SerializeToUtf8Bytes(header));
        var payloadB64 = Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{headerB64}.{payloadB64}";

        var signature = _rsa.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
