using Isopoh.Cryptography.Argon2;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Infrastructure.Security;

/// <summary>
/// Argon2id password hasher for the Identity module.
///
/// Argon2id is the current OWASP-recommended algorithm for password hashing.
/// Parameters tuned for ~300ms on a mid-range server (adjust in production):
///   - Memory cost:   65536 KiB (64 MB)
///   - Time cost:     3 iterations
///   - Parallelism:   1 (single-threaded per hash — safe for ASP.NET Core scoped services)
///   - Hash length:   32 bytes (256 bits)
///
/// Output format: Argon2 PHC string — includes algorithm, parameters, salt, and hash.
/// Example: $argon2id$v=19$m=65536,t=3,p=1$...
///
/// Architecture: Registered by IdentityModule.cs as scoped IPasswordHasher.
/// Application layer only sees IPasswordHasher — no Argon2 reference there.
/// </summary>
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const int MemoryCost   = 65536; // 64 MB
    private const int TimeCost     = 3;
    private const int Parallelism  = 1;
    private const int HashLength   = 32;    // 256-bit output

    public string Hash(string password)
    {
        // Use the library's high-level helper so the produced PHC string is guaranteed
        // to round-trip with Argon2.Verify. (The lower-level Argon2Config.EncodeString
        // path in this library version produces a string that its own Verify cannot decode.)
        return Argon2.Hash(
            password:    password,
            timeCost:    TimeCost,
            memoryCost:  MemoryCost,
            parallelism: Parallelism,
            type:        Argon2Type.HybridAddressing, // Argon2id
            hashLength:  HashLength);
    }

    public bool Verify(string password, string hash)
    {
        try
        {
            return Argon2.Verify(hash, password);
        }
        catch
        {
            // Malformed hash string — return false
            return false;
        }
    }
}
