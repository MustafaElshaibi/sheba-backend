using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Sheba.Ministry.Domain.Interfaces;

namespace Sheba.Ministry.Infrastructure.Security;

/// <summary>
/// AES-256-GCM encryption/decryption for ministry credential fields.
///
/// Format: base64( nonce[12] || ciphertext[n] || tag[16] )
///
/// Configuration key:
///   Ministry:EncryptionKey — base64-encoded 32-byte (256-bit) key.
///   Default dev key is auto-generated on first use if not configured.
/// </summary>
public sealed class AesGcmCredentialEncryptor : ICredentialEncryptor
{
    private readonly byte[] _key;

    public AesGcmCredentialEncryptor(IConfiguration configuration)
    {
        var keyBase64 = configuration["Ministry:EncryptionKey"];
        if (string.IsNullOrEmpty(keyBase64))
        {
            // Development fallback: deterministic dev key (NEVER use in production)
            _key = SHA256.HashData("sheba-ministry-dev-encryption-key-do-not-use-in-prod"u8.ToArray());
        }
        else
        {
            _key = Convert.FromBase64String(keyBase64);
        }

        if (_key.Length != 32)
            throw new InvalidOperationException("Ministry:EncryptionKey must be exactly 32 bytes (256 bits).");
    }

    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return string.Empty;

        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[12]; // AES-GCM standard nonce size
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[16]; // AES-GCM standard tag size

        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Format: nonce[12] || ciphertext[n] || tag[16]
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

        if (data.Length < 12 + 16) // nonce + tag minimum
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
