namespace Sheba.Shared.Kernel.Interfaces;

/// <summary>
/// Shared email dispatch abstraction.
///
/// Placed in Shared.Kernel so Identity.Application and other modules can inject it
/// without depending on Notification.Infrastructure directly.
///
/// Implementations live in Notification.Infrastructure:
///   • MailhogEmailProvider  — dev (Mailhog SMTP at localhost:1025)
///   • SmtpEmailProvider     — production
///
/// Switched via configuration key Notification:Email:ActiveProvider.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email. Returns true if the mail server accepted the message.
    /// Never throws on transport failure — catches and returns false.
    /// </summary>
    Task<bool> SendAsync(
        string toAddress,
        string toName,
        string subject,
        string htmlBody,
        string? textBody = null,
        CancellationToken cancellationToken = default);
}
