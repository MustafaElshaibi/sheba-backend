using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Sheba.Identity.Infrastructure.Security;

namespace Sheba.Identity.Tests.Infrastructure.Security;

/// <summary>
/// AES-256-GCM round-trip + tamper-detection for the admin TOTP secret encryptor (T-SEC-1). Uses
/// an empty IConfiguration so the dev-fallback key derivation path is exercised — the same path
/// Development uses when Identity:MfaEncryptionKey is unset.
/// </summary>
public sealed class AesGcmMfaSecretEncryptorTests
{
    private readonly AesGcmMfaSecretEncryptor _sut = new(new ConfigurationBuilder().Build());

    [Fact]
    public void EncryptThenDecrypt_RoundTrips()
    {
        const string plaintext = "JBSWY3DPEHPK3PXP";

        var ciphertext = _sut.Encrypt(plaintext);
        var decrypted = _sut.Decrypt(ciphertext);

        decrypted.Should().Be(plaintext);
        ciphertext.Should().NotBe(plaintext);
    }

    [Fact]
    public void Encrypt_SameInputTwice_ProducesDifferentCiphertext()
    {
        const string plaintext = "JBSWY3DPEHPK3PXP";

        var first = _sut.Encrypt(plaintext);
        var second = _sut.Encrypt(plaintext);

        first.Should().NotBe(second); // random nonce per call
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsCryptographicException()
    {
        var ciphertext = _sut.Encrypt("JBSWY3DPEHPK3PXP");
        var bytes = Convert.FromBase64String(ciphertext);
        bytes[^1] ^= 0xFF; // flip a bit in the auth tag
        var tampered = Convert.ToBase64String(bytes);

        var act = () => _sut.Decrypt(tampered);

        act.Should().Throw<CryptographicException>();
    }
}
