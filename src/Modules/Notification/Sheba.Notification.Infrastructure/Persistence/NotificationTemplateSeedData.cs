using Sheba.Notification.Domain.Entities;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Notification.Infrastructure.Persistence;

/// <summary>
/// The bilingual template content that used to be hardcoded inline in each Identity.Application
/// event handler (T-NOT-1). Seeded once at startup by <c>NotificationModule.SeedTemplatesAsync</c>.
///
/// Token convention: <c>{{Foo}}</c> placeholders are substituted by
/// <see cref="NotificationTemplate.Render"/>. Fields that can contain free-text citizen/admin
/// input (a rejection reason, a citizen's name) get two tokens — <c>*Html</c> (caller
/// HTML-encodes before passing in) and <c>*Text</c> (raw) — since one substitution pass feeds
/// both the HTML and plain-text bodies and encoding only belongs in the HTML one.
/// </summary>
public static class NotificationTemplateSeedData
{

    public static IEnumerable<NotificationTemplate> All() =>
    [
        NotificationTemplate.Create(
            WellKnownTemplateKeys.IdentityRequestApproved,
            subjectEn: "Your Sheba account is now active",
            subjectAr: "تم تفعيل حسابك في منصة شبى",
            bodyHtmlEn: """
                <h2>Welcome to Sheba Digital Services!</h2>
                <p>Dear citizen,</p>
                <p>Your identity verification request has been <strong>approved</strong>.</p>
                <p>You can now log in and access all Sheba e-Government services.</p>
                <p>Account ID: {{AccountId}}</p>
                """,
            bodyHtmlAr: """
                <h2>مرحبًا بك في خدمات شبى الرقمية!</h2>
                <p>عزيزي المواطن،</p>
                <p>تمت الموافقة على طلب التحقق من هويتك.</p>
                <p>يمكنك الآن تسجيل الدخول والوصول إلى جميع الخدمات الحكومية الإلكترونية.</p>
                <p>رقم الحساب: {{AccountId}}</p>
                """,
            bodyTextEn:
                "Your Sheba identity verification has been approved. You can now log in and " +
                "access all e-Government services. Account ID: {{AccountId}}",
            bodyTextAr:
                "تمت الموافقة على طلب التحقق من هويتك في شبى. يمكنك الآن تسجيل الدخول والوصول " +
                "إلى جميع الخدمات الحكومية الإلكترونية. رقم الحساب: {{AccountId}}"),

        NotificationTemplate.Create(
            WellKnownTemplateKeys.IdentityRequestRejected,
            subjectEn: "Sheba identity verification — action required",
            subjectAr: "شبى - التحقق من الهوية يتطلب إجراء",
            bodyHtmlEn: """
                <h2>Identity Verification Update</h2>
                <p>Dear citizen,</p>
                <p>We regret to inform you that your identity verification request has <strong>not been approved</strong>.</p>
                <p><strong>Reason:</strong> {{ReasonHtml}}</p>
                <p>You may submit a new request after correcting the identified issues.</p>
                <p>For assistance, contact <a href="mailto:support@sheba.gov">support@sheba.gov</a>.</p>
                """,
            bodyHtmlAr: """
                <h2>تحديث حالة التحقق من الهوية</h2>
                <p>عزيزي المواطن،</p>
                <p>يؤسفنا إبلاغك بأن طلب التحقق من هويتك لم تتم الموافقة عليه.</p>
                <p><strong>السبب:</strong> {{ReasonHtml}}</p>
                <p>يمكنك تقديم طلب جديد بعد تصحيح المشكلات المذكورة.</p>
                <p>للمساعدة، تواصل مع <a href="mailto:support@sheba.gov">support@sheba.gov</a>.</p>
                """,
            bodyTextEn: "Your Sheba identity verification request was not approved. Reason: {{ReasonText}}",
            bodyTextAr: "لم تتم الموافقة على طلب التحقق من هويتك في شبى. السبب: {{ReasonText}}"),

        NotificationTemplate.Create(
            WellKnownTemplateKeys.AccountSuspended,
            subjectEn: "⚠️ Sheba account suspended",
            subjectAr: "⚠️ تم تعليق حسابك في شبى",
            bodyHtmlEn: """
                <h2>Account Suspended</h2>
                <p>Dear citizen,</p>
                <p>Your Sheba account has been <strong>suspended</strong> and you will not be able to log in until it is reinstated.</p>
                <p><strong>Reason:</strong> {{ReasonHtml}}</p>
                <p>For assistance, contact <a href="mailto:support@sheba.gov">support@sheba.gov</a>.</p>
                """,
            bodyHtmlAr: """
                <h2>تم تعليق الحساب</h2>
                <p>عزيزي المواطن،</p>
                <p>تم <strong>تعليق</strong> حسابك في شبى ولن تتمكن من تسجيل الدخول حتى تتم إعادة تفعيله.</p>
                <p><strong>السبب:</strong> {{ReasonHtml}}</p>
                <p>للمساعدة، تواصل مع <a href="mailto:support@sheba.gov">support@sheba.gov</a>.</p>
                """,
            bodyTextEn: "Your Sheba account has been suspended. Reason: {{ReasonText}}",
            bodyTextAr: "تم تعليق حسابك في شبى. السبب: {{ReasonText}}"),

        NotificationTemplate.Create(
            WellKnownTemplateKeys.AccountReinstated,
            subjectEn: "✅ Sheba account reinstated",
            subjectAr: "✅ تمت إعادة تفعيل حسابك في شبى",
            bodyHtmlEn: """
                <h2>Account Reinstated</h2>
                <p>Dear citizen,</p>
                <p>Your Sheba account has been <strong>reinstated</strong>. You can now log in as usual.</p>
                """,
            bodyHtmlAr: """
                <h2>تمت إعادة تفعيل الحساب</h2>
                <p>عزيزي المواطن،</p>
                <p>تمت <strong>إعادة تفعيل</strong> حسابك في شبى. يمكنك الآن تسجيل الدخول كالمعتاد.</p>
                """,
            bodyTextEn: "Your Sheba account has been reinstated. You can now log in as usual.",
            bodyTextAr: "تمت إعادة تفعيل حسابك في شبى. يمكنك الآن تسجيل الدخول كالمعتاد."),

        NotificationTemplate.Create(
            WellKnownTemplateKeys.AccountDeactivated,
            subjectEn: "Sheba account deactivated",
            subjectAr: "تم إلغاء تفعيل حسابك في شبى",
            bodyHtmlEn: """
                <h2>Account Deactivated</h2>
                <p>Dear citizen,</p>
                <p>Your Sheba account has been <strong>deactivated</strong> and is no longer active.</p>
                <p><strong>Reason:</strong> {{ReasonHtml}}</p>
                <p>For assistance, contact <a href="mailto:support@sheba.gov">support@sheba.gov</a>.</p>
                """,
            bodyHtmlAr: """
                <h2>تم إلغاء تفعيل الحساب</h2>
                <p>عزيزي المواطن،</p>
                <p>تم <strong>إلغاء تفعيل</strong> حسابك في شبى ولم يعد نشطًا.</p>
                <p><strong>السبب:</strong> {{ReasonHtml}}</p>
                <p>للمساعدة، تواصل مع <a href="mailto:support@sheba.gov">support@sheba.gov</a>.</p>
                """,
            bodyTextEn: "Your Sheba account has been deactivated. Reason: {{ReasonText}}",
            bodyTextAr: "تم إلغاء تفعيل حسابك في شبى. السبب: {{ReasonText}}"),

        NotificationTemplate.Create(
            WellKnownTemplateKeys.AdminNewIdentityRequest,
            subjectEn: "🔔 New Identity Request Awaiting Review",
            subjectAr: "🔔 طلب تحقق هوية جديد بانتظار المراجعة",
            bodyHtmlEn: """
                <h2>New Identity Verification Request</h2>
                <p>A new identity verification request has been submitted and requires your review.</p>
                <table border="1" cellpadding="8" cellspacing="0" style="border-collapse:collapse;">
                  <tr><td><strong>Request ID</strong></td><td>{{RequestId}}</td></tr>
                  <tr><td><strong>Account ID</strong></td><td>{{AccountId}}</td></tr>
                  <tr><td><strong>Request Type</strong></td><td>{{RequestType}}</td></tr>
                  <tr><td><strong>Citizen Name (AR)</strong></td><td>{{FullNameArHtml}}</td></tr>
                  <tr><td><strong>Citizen Name (EN)</strong></td><td>{{FullNameEnHtml}}</td></tr>
                  <tr><td><strong>Submitted At</strong></td><td>{{SubmittedAt}}</td></tr>
                </table>
                <p>Please log in to the Sheba Admin Portal to review and decide on this request.</p>
                """,
            bodyHtmlAr: """
                <h2>طلب تحقق هوية جديد</h2>
                <p>تم تقديم طلب تحقق من الهوية جديد ويتطلب مراجعتك.</p>
                <table border="1" cellpadding="8" cellspacing="0" style="border-collapse:collapse;">
                  <tr><td><strong>رقم الطلب</strong></td><td>{{RequestId}}</td></tr>
                  <tr><td><strong>رقم الحساب</strong></td><td>{{AccountId}}</td></tr>
                  <tr><td><strong>نوع الطلب</strong></td><td>{{RequestType}}</td></tr>
                  <tr><td><strong>اسم المواطن (عربي)</strong></td><td>{{FullNameArHtml}}</td></tr>
                  <tr><td><strong>اسم المواطن (إنجليزي)</strong></td><td>{{FullNameEnHtml}}</td></tr>
                  <tr><td><strong>تاريخ التقديم</strong></td><td>{{SubmittedAt}}</td></tr>
                </table>
                <p>يرجى تسجيل الدخول إلى لوحة إدارة شبى لمراجعة هذا الطلب واتخاذ القرار.</p>
                """,
            bodyTextEn:
                "New identity request submitted for review. Request ID: {{RequestId}} | " +
                "Account: {{FullNameEnText}} ({{AccountId}}) | Type: {{RequestType}} | " +
                "Submitted: {{SubmittedAt}}. Please log in to the Sheba Admin Portal to review.",
            bodyTextAr:
                "تم تقديم طلب تحقق هوية جديد للمراجعة. رقم الطلب: {{RequestId}} | " +
                "الحساب: {{FullNameArText}} ({{AccountId}}) | النوع: {{RequestType}} | " +
                "تاريخ التقديم: {{SubmittedAt}}. يرجى تسجيل الدخول إلى لوحة إدارة شبى للمراجعة.")
    ];
}
