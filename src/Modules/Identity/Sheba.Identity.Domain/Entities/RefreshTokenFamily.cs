using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Identity.Domain.Entities;

/// <summary>
/// Tracks one refresh-token lineage for reuse detection (T-SEC-9, RFC 9700 guidance). OpenIddict
/// mints the actual refresh token after this endpoint returns, so currency isn't tracked by
/// hashing the raw token value — it's tracked by a monotonic Generation number embedded as an
/// internal (no-destination) "family_generation" claim that OpenIddict's principal-restoration
/// carries across every future refresh. A request presenting a generation that doesn't match the
/// family's current one means a superseded token was replayed — kill the whole family, not just
/// reject that one request, so an already-issued descendant token (the thief's) stops working too.
/// </summary>
public sealed class RefreshTokenFamily : BaseEntity
{
    public Guid SubjectId { get; private set; }
    public string ClientId { get; private set; } = string.Empty;
    public Guid FamilyId { get; private set; }
    public int Generation { get; private set; }
    public DateTime IssuedAt { get; private set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }
    public string? DeviceFingerprint { get; private set; }
    public string? IpAddress { get; private set; }

    private RefreshTokenFamily() { }

    public static RefreshTokenFamily Create(
        Guid subjectId,
        string clientId,
        Guid familyId,
        DateTime expiresAt,
        string? deviceFingerprint = null,
        string? ipAddress = null)
    {
        return new RefreshTokenFamily
        {
            SubjectId = subjectId,
            ClientId = clientId,
            FamilyId = familyId,
            Generation = 0,
            ExpiresAt = expiresAt,
            DeviceFingerprint = deviceFingerprint,
            IpAddress = ipAddress
        };
    }

    public bool IsRevoked => RevokedAt.HasValue;

    /// <summary>Advances to the next generation on a legitimate refresh.</summary>
    public void Rotate()
    {
        if (IsRevoked)
            throw new DomainException("Cannot rotate a revoked refresh-token family.");

        Generation++;
        IssuedAt = DateTime.UtcNow;
        Touch();
    }

    /// <summary>
    /// Kills the whole family — every request against it fails from now on, even one carrying
    /// the legitimate current generation, until the subject signs in again. Idempotent: reuse
    /// detection and an unrelated administrative action (account suspension) can both call this
    /// without one needing to know whether the other already did.
    /// </summary>
    public void Revoke(string? reason = null)
    {
        if (IsRevoked)
            return;

        RevokedAt = DateTime.UtcNow;
        RevocationReason = reason;
        Touch();
    }
}
