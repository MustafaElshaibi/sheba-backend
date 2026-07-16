namespace Sheba.Shared.Kernel.Interfaces;

/// <summary>
/// One-time-password hashing abstraction.
///
/// OTP codes are short-lived (5 min) and low-entropy (6 digits), so they use a
/// lighter Argon2id cost profile than passwords. The raw code is NEVER stored —
/// only the salted Argon2id hash. Verification uses <see cref="Verify"/> (constant-time,
/// salt-aware) rather than re-hash-and-compare, because Argon2id hashes are salted
/// and therefore non-deterministic.
///
/// Implementation lives in Identity.Infrastructure. The Application layer only sees
/// this interface (Clean Architecture: Application → Kernel only).
/// </summary>
public interface IOtpHasher
{
    /// <summary>Hashes a raw OTP code. Returns the full encoded Argon2id string.</summary>
    string Hash(string rawCode);

    /// <summary>Verifies a raw OTP code against a stored hash. Never throws on malformed input.</summary>
    bool Verify(string rawCode, string hash);
}
