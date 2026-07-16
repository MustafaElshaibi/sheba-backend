using Sheba.Shared.Kernel.Entities;

namespace Sheba.Identity.Domain.Entities;

public sealed class RefreshTokenFamily : BaseEntity
{
    public Guid AccountId { get; private set; }
    public string ClientId { get; private set; } = string.Empty;
    public Guid FamilyId { get; private set; }
    public string CurrentTokenHash { get; private set; } = string.Empty;
    public DateTime IssuedAt { get; private set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }
    public string? DeviceFingerprint { get; private set; }
    public string? IpAddress { get; private set; }

    private RefreshTokenFamily() { }

    public static RefreshTokenFamily Create(
        Guid accountId,
        string clientId,
        Guid familyId,
        string currentTokenHash,
        DateTime expiresAt,
        string? deviceFingerprint = null,
        string? ipAddress = null)
    {
        return new RefreshTokenFamily
        {
            AccountId = accountId,
            ClientId = clientId,
            FamilyId = familyId,
            CurrentTokenHash = currentTokenHash,
            ExpiresAt = expiresAt,
            DeviceFingerprint = deviceFingerprint,
            IpAddress = ipAddress
        };
    }

    public void Rotate(string newTokenHash)
    {
        CurrentTokenHash = newTokenHash;
        IssuedAt = DateTime.UtcNow;
        Touch();
    }

    public void Revoke(string? reason = null)
    {
        RevokedAt = DateTime.UtcNow;
        RevocationReason = reason;
        Touch();
    }
}