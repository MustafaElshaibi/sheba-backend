namespace Sheba.Notification.Domain.Interfaces;

/// <summary>
/// Port for the email dispatch adapter.
/// Implementations: MailhogEmailProvider (dev), SmtpEmailProvider (production).
/// Switched via configuration key Notification:Email:ActiveProvider.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends a plain-text + HTML email.
    /// Returns true if accepted by the mail server (does NOT guarantee delivery).
    /// Never throws on infrastructure failure — returns false and logs instead.
    /// </summary>
    Task<bool> SendAsync(
        string toAddress,
        string toName,
        string subject,
        string htmlBody,
        string? textBody = null,
        CancellationToken cancellationToken = default);
}
