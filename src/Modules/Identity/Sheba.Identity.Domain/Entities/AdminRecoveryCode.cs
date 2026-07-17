using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Identity.Domain.Entities;

/// <summary>
/// One single-use MFA recovery code, issued in a batch of 10 when an admin confirms TOTP
/// enrollment. Lets an admin sign in if their authenticator device is lost. The raw code is
/// NEVER stored — only its Argon2id hash (same treatment as OtpRecord/PasswordHash).
/// </summary>
public sealed class AdminRecoveryCode : BaseEntity
{
    public Guid AdminUserId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public DateTime? UsedAt { get; private set; }

    private AdminRecoveryCode() { }

    public static AdminRecoveryCode Create(Guid adminUserId, string codeHash) =>
        new()
        {
            AdminUserId = adminUserId,
            CodeHash = codeHash
        };

    /// <summary>
    /// Canonical form used both when hashing a freshly-issued code and when matching a
    /// caller-supplied one — strips formatting (the "-" separator, stray whitespace) and
    /// case, so "ab3d9-k7q2m", "AB3D9K7Q2M", and "ab3d9 k7q2m" all compare equal.
    /// </summary>
    public static string Normalize(string raw) =>
        new string(raw.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    public bool IsUsed => UsedAt.HasValue;

    public void MarkUsed()
    {
        if (IsUsed)
            throw new DomainException("Recovery code has already been used.");

        UsedAt = DateTime.UtcNow;
        Touch();
    }
}
