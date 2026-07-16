using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Notification.Infrastructure.Adapters;

/// <summary>
/// Development SMTP email provider that sends via a real SMTP relay.
/// Used in staging/production with TLS authentication.
///
/// Configuration keys (appsettings.json / secrets):
///   Notification:Email:Host        → "smtp.sendgrid.net"
///   Notification:Email:Port        → 587
///   Notification:Email:Username    → "apikey"
///   Notification:Email:Password    → (from secrets)
///   Notification:Email:FromAddress → "noreply@sheba.gov.eg"
///   Notification:Email:FromName    → "Sheba Government Services"
/// </summary>
public sealed class SmtpEmailProvider(
    IConfiguration configuration,
    ILogger<SmtpEmailProvider> logger
) : IEmailService
{
    private string Host        => configuration["Notification:Email:Host"]        ?? "localhost";
    private int    Port        => int.TryParse(configuration["Notification:Email:Port"], out var p) ? p : 587;
    private string Username    => configuration["Notification:Email:Username"]    ?? string.Empty;
    private string Password    => configuration["Notification:Email:Password"]    ?? string.Empty;
    private string FromAddress => configuration["Notification:Email:FromAddress"] ?? "noreply@sheba.gov";
    private string FromName    => configuration["Notification:Email:FromName"]    ?? "Sheba";

    public async Task<bool> SendAsync(
        string toAddress,
        string toName,
        string subject,
        string htmlBody,
        string? textBody = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new MimeKit.MimeMessage();
            message.From.Add(new MimeKit.MailboxAddress(FromName, FromAddress));
            message.To.Add(new MimeKit.MailboxAddress(toName, toAddress));
            message.Subject = subject;

            var bodyBuilder = new MimeKit.BodyBuilder
            {
                HtmlBody = htmlBody,
                TextBody = textBody ?? htmlBody
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            await smtp.ConnectAsync(Host, Port, MailKit.Security.SecureSocketOptions.StartTls, cancellationToken);
            if (!string.IsNullOrEmpty(Username))
                await smtp.AuthenticateAsync(Username, Password, cancellationToken);
            await smtp.SendAsync(message, cancellationToken);
            await smtp.DisconnectAsync(quit: true, cancellationToken);

            logger.LogInformation("[SmtpEmail] Email sent to {To}: {Subject}", toAddress, subject);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "[SmtpEmail] Failed to send email to {To}: {Subject}", toAddress, subject);
            return false;
        }
    }
}
