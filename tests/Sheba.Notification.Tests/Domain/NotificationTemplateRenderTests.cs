using FluentAssertions;
using Sheba.Notification.Domain.Entities;

namespace Sheba.Notification.Tests.Domain;

public sealed class NotificationTemplateRenderTests
{
    private static NotificationTemplate BuildTemplate(
        string key = "TestKey",
        string subjectEn = "Hello {{Name}}",
        string subjectAr = "مرحبا {{Name}}",
        string bodyHtmlEn = "<p>Hi {{Name}}</p>",
        string bodyHtmlAr = "<p>أهلاً {{Name}}</p>",
        string bodyTextEn = "Hi {{Name}}",
        string bodyTextAr = "أهلاً {{Name}}") =>
        NotificationTemplate.Create(key, subjectEn, subjectAr, bodyHtmlEn, bodyHtmlAr, bodyTextEn, bodyTextAr);

    [Fact]
    public void Render_SubstitutesToken_InSubjectAndBothBodies()
    {
        var template = BuildTemplate();
        var tokens = new Dictionary<string, string> { ["Name"] = "Ahmed" };

        var result = template.Render(tokens);

        result.Subject.Should().Be("Hello Ahmed / مرحبا Ahmed");
        result.HtmlBody.Should().Contain("<p>Hi Ahmed</p>");
        result.HtmlBody.Should().Contain("<p>أهلاً Ahmed</p>");
        result.TextBody.Should().Contain("Hi Ahmed");
        result.TextBody.Should().Contain("أهلاً Ahmed");
    }

    [Fact]
    public void Render_CombinesSubject_AsEnSlashAr()
    {
        var template = BuildTemplate(subjectEn: "Welcome", subjectAr: "أهلاً");
        var result = template.Render(new Dictionary<string, string>());

        result.Subject.Should().Be("Welcome / أهلاً");
    }

    [Fact]
    public void Render_HtmlBody_HasLtrAndRtlDivs()
    {
        var template = BuildTemplate();
        var result = template.Render(new Dictionary<string, string> { ["Name"] = "X" });

        result.HtmlBody.Should().Contain("direction:ltr");
        result.HtmlBody.Should().Contain("direction:rtl");
    }

    [Fact]
    public void Render_TextBody_ContainsBothLanguagesSeparatedByDashes()
    {
        var template = BuildTemplate(bodyTextEn: "English", bodyTextAr: "عربي");
        var result = template.Render(new Dictionary<string, string>());

        result.TextBody.Should().Be("English\n---\nعربي");
    }

    [Fact]
    public void Render_EmptyTokens_LeavesPlaceholdersUnchanged()
    {
        var template = BuildTemplate(subjectEn: "Hello {{Name}}", subjectAr: "مرحبا");
        var result = template.Render(new Dictionary<string, string>());

        result.Subject.Should().Contain("{{Name}}");
    }

    [Fact]
    public void Render_MultipleTokens_AllSubstituted()
    {
        var template = BuildTemplate(
            subjectEn: "{{Greeting}} {{Name}}",
            subjectAr: "{{Greeting}} {{Name}}");

        var result = template.Render(new Dictionary<string, string>
        {
            ["Greeting"] = "Hello",
            ["Name"] = "citizen"
        });

        result.Subject.Should().StartWith("Hello citizen");
    }

    [Fact]
    public void Render_HtmlEncodedToken_NotDoubleEncoded()
    {
        var template = BuildTemplate(bodyHtmlEn: "<p>{{Reason}}</p>", bodyHtmlAr: "");
        // Caller is responsible for HTML-encoding user input before passing it in
        var encoded = System.Net.WebUtility.HtmlEncode("<script>alert(1)</script>");
        var result = template.Render(new Dictionary<string, string> { ["Reason"] = encoded });

        result.HtmlBody.Should().Contain("&lt;script&gt;");
        result.HtmlBody.Should().NotContain("<script>");
    }

    [Fact]
    public void Create_EmptyKey_ThrowsDomainException()
    {
        var act = () => NotificationTemplate.Create("", "s", "s", "b", "b", "t", "t");
        act.Should().Throw<Sheba.Shared.Kernel.Exceptions.DomainException>();
    }
}
