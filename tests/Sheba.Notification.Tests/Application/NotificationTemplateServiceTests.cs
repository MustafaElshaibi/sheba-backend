using FluentAssertions;
using NSubstitute;
using Sheba.Notification.Domain.Entities;
using Sheba.Notification.Domain.Interfaces;
using Sheba.Notification.Infrastructure.Services;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Notification.Tests.Application;

public sealed class NotificationTemplateServiceTests
{
    private readonly INotificationTemplateRepository _repo =
        Substitute.For<INotificationTemplateRepository>();

    private readonly NotificationTemplateService _sut;

    public NotificationTemplateServiceTests() => _sut = new NotificationTemplateService(_repo);

    private static NotificationTemplate ApprovalTemplate() =>
        NotificationTemplate.Create(
            WellKnownTemplateKeys.IdentityRequestApproved,
            "Account active", "الحساب نشط",
            "<p>Active {{AccountId}}</p>", "<p>نشط {{AccountId}}</p>",
            "Active {{AccountId}}", "نشط {{AccountId}}");

    [Fact]
    public async Task RenderAsync_KnownKey_ReturnsRenderedNotification()
    {
        _repo.GetByKeyAsync(WellKnownTemplateKeys.IdentityRequestApproved, default)
             .Returns(ApprovalTemplate());

        var result = await _sut.RenderAsync(
            WellKnownTemplateKeys.IdentityRequestApproved,
            new Dictionary<string, string> { ["AccountId"] = "abc-123" });

        result.Should().NotBeNull();
        result.Subject.Should().Contain("Account active");
        result.HtmlBody.Should().Contain("abc-123");
        result.TextBody.Should().Contain("abc-123");
    }

    [Fact]
    public async Task RenderAsync_UnknownKey_ThrowsInvalidOperationException()
    {
        _repo.GetByKeyAsync("NonExistentKey", default).Returns((NotificationTemplate?)null);

        var act = async () => await _sut.RenderAsync(
            "NonExistentKey",
            new Dictionary<string, string>());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*NonExistentKey*");
    }

    [Fact]
    public async Task RenderAsync_PassesTokensThroughToTemplate()
    {
        _repo.GetByKeyAsync(Arg.Any<string>(), default).Returns(ApprovalTemplate());

        var result = await _sut.RenderAsync(
            WellKnownTemplateKeys.IdentityRequestApproved,
            new Dictionary<string, string> { ["AccountId"] = "specific-guid-value" });

        result.HtmlBody.Should().Contain("specific-guid-value");
        result.TextBody.Should().Contain("specific-guid-value");
    }
}
