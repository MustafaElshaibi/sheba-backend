using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Sheba.Identity.Domain.Interfaces;

namespace Sheba.Identity.Infrastructure.Security;

/// <summary>
/// AES-256-GCM encryption/decryption for AdminUser TOTP secrets. Same primitive and wire format
/// as Ministry's AesGcmCredentialEncryptor (format: base64(nonce[12] || ciphertext[n] || tag[16])),
/// duplicated locally because Identity cannot reference Ministry's Domain assembly.
///
/// Configuration key:
///   Identity:MfaEncryptionKey — base64-encoded 32-byte (256-bit) key.
///   Development fallback: deterministic dev key (NEVER use in production) — same accepted
///   limitation as Ministry's encryptor, tracked under T-SEC-3.
/// </summary>
public sealed class AesGcmMfaSecretEncryptor : IMfaSecretEncryptor
{
    private readonly byte[] _key;

    public AesGcmMfaSecretEncryptor(IConfiguration configuration)
    {
        var keyBase64 = configuration["Identity:MfaEncryptionKey"];
        if (string.IsNullOrEmpty(keyBase64))
        {
            _key = SHA256.HashData("sheba-identity-dev-mfa-encryption-key-do-not-use-in-prod"u8.ToArray());
        }
        else
        {
            _key = Convert.FromBase64String(keyBase64);
        }

        if (_key.Length != 32)
            throw new InvalidOperationException("Identity:MfaEncryptionKey must be exactly 32 bytes (256 bits).");
    }

    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return string.Empty;

        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var result = new byte[nonce.Length + ciphertext.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(ciphertext, 0, result, nonce.Length, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length + ciphertext.Length, tag.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string ciphertextBase64)
    {
        if (string.IsNullOrEmpty(ciphertextBase64))
            return string.Empty;

        var data = Convert.FromBase64String(ciphertextBase64);

        if (data.Length < 12 + 16)
            throw new CryptographicException("Ciphertext is too short to contain nonce and tag.");

        var nonce = data[..12];
        var tag = data[^16..];
        var ciphertext = data[12..^16];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return System.Text.Encoding.UTF8.GetString(plaintext);
    }
}
