namespace Sheba.Shared.Kernel.Interfaces;

/// <summary>
/// Password hashing abstraction used by Identity.Application handlers.
/// Implementations live in Identity.Infrastructure using Argon2id.
///
/// The Application layer NEVER imports a concrete hasher — it only uses this interface.
/// This maintains the Clean Architecture boundary (Application → Kernel only, not Infrastructure).
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Computes a password hash. Returns the full encoded string (algorithm + params + salt + hash).
    /// Thread-safe — may be called from any thread.
    /// </summary>
    string Hash(string password);

    /// <summary>
    /// Verifies a password against a stored hash.
    /// Returns true if the password matches; false otherwise.
    /// Never throws on malformed input.
    /// </summary>
    bool Verify(string password, string hash);
}
