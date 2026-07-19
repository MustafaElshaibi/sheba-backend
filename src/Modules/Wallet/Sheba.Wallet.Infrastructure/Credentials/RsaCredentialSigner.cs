using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sheba.Wallet.Domain.Interfaces;

namespace Sheba.Wallet.Infrastructure.Credentials;

/// <summary>
/// Builds W3C Verifiable Credentials as RS256-signed JWTs using System.Security.Cryptography.
///
/// The RSA private key is read from configuration (Wallet:IssuerPrivateKeyPem).
/// If absent in Development, an ephemeral key is generated — VCs issued in that session will
/// NOT verify after a restart (T-WAL-1). In non-Development the module refuses to start
/// without a configured key (enforced in WalletModule.AddWalletModule).
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

    public RsaCredentialSigner(IConfiguration configuration, ILogger<RsaCredentialSigner> logger)
    {
        IssuerDid = configuration["Wallet:IssuerDid"] ?? "did:sheba:issuer";

        _rsa = RSA.Create(2048);
        var privatePem = configuration["Wallet:IssuerPrivateKeyPem"];
        if (!string.IsNullOrWhiteSpace(privatePem))
        {
            _rsa.ImportFromPem(privatePem);
        }
        else
        {
            // WalletModule.AddWalletModule already blocks non-Development from reaching this
            // path. The warning below is a belt-and-suspenders notice for dev/test sessions.
            logger.LogWarning(
                "[Wallet] Wallet:IssuerPrivateKeyPem is not configured. " +
                "Using an ephemeral RSA key — VCs issued in this session will NOT verify " +
                "after a restart. Set Wallet:IssuerPrivateKeyPem before going to production.");
        }

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

    public bool VerifyIssuerSignature(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3)
            return false;

        try
        {
            var signingInput = $"{parts[0]}.{parts[1]}";
            var signature = Base64UrlDecode(parts[2]);
            return _rsa.VerifyData(
                Encoding.ASCII.GetBytes(signingInput), signature,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
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
