namespace Sheba.Ministry.Domain.Interfaces;

/// <summary>
/// AES-256-GCM encryption/decryption for ministry credential fields.
/// All sensitive data (API keys, tokens, passwords, client secrets) are encrypted
/// at rest. This interface is implemented in Infrastructure.
///
/// Key management: The encryption key is read from configuration:
///   Ministry:EncryptionKey — base64-encoded 32-byte (256-bit) AES key.
///   In production, this should come from Azure Key Vault / AWS KMS / HashiCorp Vault.
/// </summary>
public interface ICredentialEncryptor
{
    /// <summary>Encrypts plaintext to a base64 AES-256-GCM ciphertext string (includes nonce + tag).</summary>
    string Encrypt(string plaintext);

    /// <summary>Decrypts a base64 AES-256-GCM ciphertext string back to plaintext.</summary>
    string Decrypt(string ciphertext);
}
