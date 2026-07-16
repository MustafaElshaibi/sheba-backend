using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Ministry.Domain.Entities;

/// <summary>
/// Webhook registration — ministry systems calling Sheba back.
/// The signing secret is encrypted at rest for HMAC signature verification.
/// </summary>
public sealed class MinistryWebhook : BaseEntity
{
    public Guid MinistryId { get; private set; }
    public Guid? EndpointId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string ShebaWebhookPath { get; private set; } = string.Empty;
    public string SigningSecret { get; private set; } = string.Empty;  // encrypted
    public bool IsActive { get; private set; } = true;
    public DateTime? LastReceivedAt { get; private set; }

    // EF Core
    private MinistryWebhook() { }

    public static MinistryWebhook Create(
        Guid ministryId,
        string eventType,
        string shebaWebhookPath,
        string encryptedSigningSecret,
        Guid? endpointId = null)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new DomainException("Webhook event type is required.");
        if (string.IsNullOrWhiteSpace(shebaWebhookPath))
            throw new DomainException("Sheba webhook path is required.");
        if (string.IsNullOrWhiteSpace(encryptedSigningSecret))
            throw new DomainException("Signing secret is required.");

        return new MinistryWebhook
        {
            MinistryId = ministryId,
            EndpointId = endpointId,
            EventType = eventType.Trim(),
            ShebaWebhookPath = shebaWebhookPath.Trim(),
            SigningSecret = encryptedSigningSecret
        };
    }

    public void RecordReceived()
    {
        LastReceivedAt = DateTime.UtcNow;
        Touch();
    }

    public void Activate() { IsActive = true; Touch(); }
    public void Deactivate() { IsActive = false; Touch(); }
}
