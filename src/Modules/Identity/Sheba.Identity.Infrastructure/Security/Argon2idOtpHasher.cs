using Isopoh.Cryptography.Argon2;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Infrastructure.Security;

/// <summary>
/// Argon2id OTP hasher.
///
/// Uses a deliberately lighter cost profile than <see cref="Argon2idPasswordHasher"/>
/// because OTP codes are short-lived (5-minute TTL) and rate-limited, so the threat model
/// is weaker than long-lived password storage. This keeps login/registration latency low
/// while still never storing the plaintext code.
///
/// Uses the library's high-level Argon2.Hash/Verify helpers (the low-level
/// Argon2Config.EncodeString path in this library version produces a string its own
/// Verify cannot decode).
/// </summary>
public sealed class Argon2idOtpHasher : IOtpHasher
{
    private const int MemoryCost  = 8192; // 8 MB — light; OTPs expire in 5 min
    private const int TimeCost    = 2;
    private const int Parallelism = 1;
    private const int HashLength  = 32;

    public string Hash(string rawCode) =>
        Argon2.Hash(
            password:    rawCode,
            timeCost:    TimeCost,
            memoryCost:  MemoryCost,
            parallelism: Parallelism,
            type:        Argon2Type.HybridAddressing, // Argon2id
            hashLength:  HashLength);

    public bool Verify(string rawCode, string hash)
    {
        try
        {
            return Argon2.Verify(hash, rawCode);
        }
        catch
        {
            return false;
        }
    }
}
