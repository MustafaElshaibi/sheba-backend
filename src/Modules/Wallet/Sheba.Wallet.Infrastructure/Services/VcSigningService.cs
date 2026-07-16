using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sheba.Wallet.Application.Interfaces;

namespace Sheba.Wallet.Infrastructure.Services;

/// <summary>
/// RSA-based JWT-VC signing service implemented using only BCL types
/// (System.Security.Cryptography + System.Text.Json — no extra NuGet packages).
///
/// Produces a compact JWT string conforming to the W3C VC-JWT spec.
/// Key auto-generated on first run and persisted as PEM so tokens survive restarts.
///
/// For production: swap key storage to Azure Key Vault / HashiCorp Vault.
/// </summary>
public sealed class VcSigningService : IVcSigningService
{
    private readonly RSA _rsa;
    private readonly string _keyId;
    private readonly ILogger<VcSigningService> _logger;

    private const string KeyFileName = "vc-signing-key.pem";

    public VcSigningService(ILogger<VcSigningService> logger)
    {
        _logger = logger;
        _keyId  = "sheba-vc-key-1";
        _rsa    = RSA.Create(2048);

        var keyPath = Path.Combine(AppContext.BaseDirectory, KeyFileName);

        if (File.Exists(keyPath))
        {
            _rsa.ImportFromPem(File.ReadAllText(keyPath));
            _logger.LogInformation("[VcSigning] Loaded existing RSA key from {Path}", keyPath);
        }
        else
        {
            File.WriteAllText(keyPath, _rsa.ExportRSAPrivateKeyPem());
            _logger.LogInformation("[VcSigning] Generated new RSA-2048 key → {Path}", keyPath);
        }
    }

    public string SignCredentialJwt(
        string issuerDid,
        string subjectDid,
        string credentialId,
        string credentialType,
        Dictionary<string, object> claims,
        DateTime issuedAt,
        DateTime expiresAt)
    {
        // ── Header ─────────────────────────────────────────────────────────
        var header = new Dictionary<string, object>
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT",
            ["kid"] = _keyId
        };

        // ── VC payload (W3C VC-JWT structure) ──────────────────────────────
        var credentialSubject = new Dictionary<string, object>(claims)
        {
            ["id"] = subjectDid
        };

        var vcClaim = new Dictionary<string, object>
        {
            ["@context"] = new[] { "https://www.w3.org/2018/credentials/v1" },
            ["type"]     = new[] { "VerifiableCredential", credentialType },
            ["credentialSubject"] = credentialSubject
        };

        var payload = new Dictionary<string, object>
        {
            ["iss"] = issuerDid,
            ["sub"] = subjectDid,
            ["jti"] = credentialId,
            ["iat"] = new DateTimeOffset(issuedAt).ToUnixTimeSeconds(),
            ["exp"] = new DateTimeOffset(expiresAt).ToUnixTimeSeconds(),
            ["nbf"] = new DateTimeOffset(issuedAt).ToUnixTimeSeconds(),
            ["vc"]  = vcClaim
        };

        // ── Build signing input ─────────────────────────────────────────────
        var headerB64  = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        var payloadB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{headerB64}.{payloadB64}";

        // ── Sign with RSA-SHA256 ────────────────────────────────────────────
        var inputBytes = Encoding.UTF8.GetBytes(signingInput);
        var signature  = _rsa.SignData(inputBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{signingInput}.{Base64UrlEncode(signature)}";
    }

    public string GetPublicKeyJwk()
    {
        var p = _rsa.ExportParameters(false);
        var jwk = new Dictionary<string, object>
        {
            ["kty"] = "RSA",
            ["kid"] = _keyId,
            ["use"] = "sig",
            ["alg"] = "RS256",
            ["n"]   = Base64UrlEncode(p.Modulus!),
            ["e"]   = Base64UrlEncode(p.Exponent!)
        };
        return JsonSerializer.Serialize(jwk);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
