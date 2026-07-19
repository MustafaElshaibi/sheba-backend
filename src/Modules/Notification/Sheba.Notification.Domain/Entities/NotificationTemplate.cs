using Sheba.Shared.Kernel.Entities;
using Sheba.Shared.Kernel.Exceptions;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Notification.Domain.Entities;

/// <summary>
/// A bilingual (English + Arabic) notification template, keyed by a stable string other modules
/// reference when triggering a send (T-NOT-1). Replaces the hardcoded English-only HTML/text
/// strings that used to live inline in each event handler.
/// </summary>
public sealed class NotificationTemplate : BaseEntity
{
    public string TemplateKey { get; private set; } = string.Empty;
    public string SubjectEn { get; private set; } = string.Empty;
    public string SubjectAr { get; private set; } = string.Empty;
    public string BodyHtmlEn { get; private set; } = string.Empty;
    public string BodyHtmlAr { get; private set; } = string.Empty;
    public string BodyTextEn { get; private set; } = string.Empty;
    public string BodyTextAr { get; private set; } = string.Empty;

    private NotificationTemplate() { }

    public static NotificationTemplate Create(
        string templateKey,
        string subjectEn, string subjectAr,
        string bodyHtmlEn, string bodyHtmlAr,
        string bodyTextEn, string bodyTextAr)
    {
        if (string.IsNullOrWhiteSpace(templateKey))
            throw new DomainException("Template key is required.");

        return new NotificationTemplate
        {
            TemplateKey = templateKey,
            SubjectEn = subjectEn,
            SubjectAr = subjectAr,
            BodyHtmlEn = bodyHtmlEn,
            BodyHtmlAr = bodyHtmlAr,
            BodyTextEn = bodyTextEn,
            BodyTextAr = bodyTextAr
        };
    }

    /// <summary>
    /// Substitutes <c>{{TokenName}}</c> placeholders and combines both languages into one
    /// bilingual email — there's no per-citizen language preference to pick from today, so both
    /// are always shown (English first, Arabic in an RTL block below).
    /// </summary>
    public RenderedNotification Render(IReadOnlyDictionary<string, string> tokens)
    {
        string html = $"""
            <div style="direction:ltr;text-align:left;">{Substitute(BodyHtmlEn, tokens)}</div>
            <hr/>
            <div dir="rtl" style="direction:rtl;text-align:right;">{Substitute(BodyHtmlAr, tokens)}</div>
            """;

        string text = $"{Substitute(BodyTextEn, tokens)}\n---\n{Substitute(BodyTextAr, tokens)}";

        string subject = $"{Substitute(SubjectEn, tokens)} / {Substitute(SubjectAr, tokens)}";

        return new RenderedNotification(subject, html, text);
    }

    private static string Substitute(string template, IReadOnlyDictionary<string, string> tokens)
    {
        foreach (var (key, value) in tokens)
            template = template.Replace("{{" + key + "}}", value);
        return template;
    }
}
