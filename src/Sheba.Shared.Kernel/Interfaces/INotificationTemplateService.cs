namespace Sheba.Shared.Kernel.Interfaces;

/// <summary>
/// Cross-module port over the Notification module's bilingual template registry (T-NOT-1).
/// Placed in Shared.Kernel so Identity.Application and other modules can render a notification
/// without depending on Notification.Domain/Infrastructure directly (rule 1/3, mirrors
/// IEmailService/ISmsService). Implemented in Notification.Infrastructure.
/// </summary>
public interface INotificationTemplateService
{
    /// <summary>
    /// Renders the named template with the given tokens (each <c>{{TokenName}}</c> placeholder
    /// in the template is substituted). Throws <see cref="InvalidOperationException"/> if the key
    /// isn't seeded — a missing template is a deployment bug, not something callers should
    /// silently work around with an inline fallback string.
    /// </summary>
    Task<RenderedNotification> RenderAsync(
        string templateKey, IReadOnlyDictionary<string, string> tokens, CancellationToken ct = default);
}

/// <summary>A rendered bilingual notification, ready to hand to <see cref="IEmailService"/>.</summary>
public sealed record RenderedNotification(string Subject, string HtmlBody, string TextBody);

/// <summary>
/// Stable key strings shared between the Notification module (which seeds the templates) and any
/// module that calls <see cref="INotificationTemplateService.RenderAsync"/> (e.g. Identity).
/// Both sides import from Shared.Kernel — no cross-module assembly reference needed.
/// </summary>
public static class WellKnownTemplateKeys
{
    public const string IdentityRequestApproved = "IdentityRequestApproved";
    public const string IdentityRequestRejected = "IdentityRequestRejected";
    public const string AccountSuspended        = "AccountSuspended";
    public const string AccountReinstated       = "AccountReinstated";
    public const string AccountDeactivated      = "AccountDeactivated";
    public const string AdminNewIdentityRequest = "AdminNewIdentityRequest";
}
