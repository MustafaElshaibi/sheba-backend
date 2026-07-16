using Sheba.Shared.Kernel.Entities;

namespace Sheba.Notification.Domain.Entities;

/// <summary>
/// Append-only audit record of every outbound notification (email or SMS).
/// Once created, a NotificationRecord is never modified (immutable log).
///
/// Architecture: Notification module owns this entity exclusively.
/// Other modules trigger notifications via MediatR INotification events —
/// they never reference NotificationRecord directly.
/// </summary>
public sealed class NotificationRecord : BaseEntity
{
    public Guid RecipientId { get; private set; }   // AccountId or external recipient
    public string Channel { get; private set; } = string.Empty;   // "Email" | "Sms"
    public string Recipient { get; private set; } = string.Empty; // email address or phone
    public string Subject { get; private set; } = string.Empty;   // email subject / SMS tag
    public string Body { get; private set; } = string.Empty;
    public bool   Succeeded { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime SentAt { get; private set; }

    // EF Core
    private NotificationRecord() { }

    public static NotificationRecord CreateEmail(
        Guid recipientId,
        string toAddress,
        string subject,
        string body,
        bool succeeded,
        string? error = null)
    {
        return new NotificationRecord
        {
            RecipientId  = recipientId,
            Channel      = "Email",
            Recipient    = toAddress,
            Subject      = subject,
            Body         = body,
            Succeeded    = succeeded,
            ErrorMessage = error,
            SentAt       = DateTime.UtcNow
        };
    }

    public static NotificationRecord CreateSms(
        Guid recipientId,
        string toPhone,
        string message,
        bool succeeded,
        string? error = null)
    {
        return new NotificationRecord
        {
            RecipientId  = recipientId,
            Channel      = "Sms",
            Recipient    = toPhone,
            Subject      = "(SMS)",
            Body         = message,
            Succeeded    = succeeded,
            ErrorMessage = error,
            SentAt       = DateTime.UtcNow
        };
    }
}
