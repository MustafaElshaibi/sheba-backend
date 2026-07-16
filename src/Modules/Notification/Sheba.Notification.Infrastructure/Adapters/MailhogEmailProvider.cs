using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Notification.Infrastructure.Adapters;

/// <summary>
/// Development email provider that sends to Mailhog (SMTP trap at localhost:1025).
/// Mailhog captures all emails and exposes them on http://localhost:8025.
///
/// Configuration keys (appsettings.Development.json):
///   Notification:Email:Host        → "localhost"
///   Notification:Email:Port        → 1025
///   Notification:Email:FromAddress → "noreply@sheba.dev"
///   Notification:Email:FromName    → "Sheba Dev"
///
/// Docker: docker run -d -p 1025:1025 -p 8025:8025 mailhog/mailhog
/// </summary>
public sealed class MailhogEmailProvider(
    IConfiguration configuration,
    ILogger<MailhogEmailProvider> logger
) : IEmailService
{
    private string Host        => configuration["Notification:Email:Host"]        ?? "localhost";
    private int    Port        => int.TryParse(configuration["Notification:Email:Port"], out var p) ? p : 1025;
    private string FromAddress => configuration["Notification:Email:FromAddress"] ?? "noreply@sheba.dev";
    private string FromName    => configuration["Notification:Email:FromName"]    ?? "Sheba Dev";

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
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(FromName, FromAddress));
            message.To.Add(new MailboxAddress(toName, toAddress));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody  = htmlBody,
                TextBody  = textBody ?? StripHtml(htmlBody)
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var smtp = new SmtpClient();
            // Mailhog accepts cleartext — no SSL, no auth
            await smtp.ConnectAsync(Host, Port, MailKit.Security.SecureSocketOptions.None, cancellationToken);
            await smtp.SendAsync(message, cancellationToken);
            await smtp.DisconnectAsync(quit: true, cancellationToken);

            logger.LogInformation(
                "[MailhogEmail] Email sent to {To}: {Subject}",
                toAddress, subject);

            return true;
        }
        catch (OperationCanceledException)
        {
            throw; // propagate cancellation
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[MailhogEmail] Failed to send email to {To}: {Subject}",
                toAddress, subject);
            return false;
        }
    }

    /// <summary>Strips basic HTML tags for the plain-text fallback body.</summary>
    private static string StripHtml(string html)
    {
        // Simple regex-free strip — sufficient for our templates
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s{2,}", " ");
        return text.Trim();
    }
}
