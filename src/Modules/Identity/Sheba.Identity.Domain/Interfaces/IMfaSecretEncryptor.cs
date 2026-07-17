namespace Sheba.Identity.Domain.Interfaces;

/// <summary>
/// Encrypts/decrypts an AdminUser's TOTP secret at rest. Mirrors the Ministry module's
/// ICredentialEncryptor (AES-256-GCM) — duplicated rather than shared because a module may only
/// depend on Sheba.Shared.Kernel, never another module's Domain assembly.
/// Implementation lives in Identity.Infrastructure.
/// </summary>
public interface IMfaSecretEncryptor
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertextBase64);
}
