using System.Text.Json;
using FluentAssertions;
using Sheba.Audit.Infrastructure.Behaviors;

namespace Sheba.Audit.Tests.Behaviors;

public class AuditSnapshotRedactorTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed record LoginCommand(string Username, string Password, string? Otp);
    private sealed record RegisterCommand(string NationalId, string PhoneNumber, string Password);
    private sealed record AdminMfaCommand(Guid AdminId, string TotpCode, string RecoveryCodes);
    private sealed record SafeShapedResponse(Guid AccountId, string MaskedPhone, string Status);
    private sealed record FutureFieldCommand(Guid AccountId, string SomeBrandNewField);

    [Fact]
    public void Redact_CommandWithPassword_PasswordFieldRedacted()
    {
        var command = new LoginCommand("citizen1", "super-secret-pw", "123456");

        var json = AuditSnapshotRedactor.Redact(command, Options);

        json.Should().NotContain("super-secret-pw");
        json.Should().NotContain("123456");
        json.Should().Contain("\"password\":\"[REDACTED]\"");
        json.Should().Contain("\"otp\":\"[REDACTED]\"");
    }

    [Fact]
    public void Redact_CommandWithNationalIdAndPhone_BothRedacted()
    {
        var command = new RegisterCommand("01019001234", "+967700000000", "another-secret");

        var json = AuditSnapshotRedactor.Redact(command, Options);

        json.Should().NotContain("01019001234");
        json.Should().NotContain("+967700000000");
        json.Should().NotContain("another-secret");
    }

    [Fact]
    public void Redact_AdminMfaCommand_TotpAndRecoveryCodesRedactedButAdminIdKept()
    {
        var adminId = Guid.NewGuid();
        var command = new AdminMfaCommand(adminId, "654321", "AAAA-BBBB,CCCC-DDDD");

        var json = AuditSnapshotRedactor.Redact(command, Options);

        json.Should().NotContain("654321");
        json.Should().NotContain("AAAA-BBBB");
        json.Should().Contain(adminId.ToString());
    }

    [Fact]
    public void Redact_ResponseWithOnlySafeFields_ValuesPreservedVerbatim()
    {
        var accountId = Guid.NewGuid();
        var response = new SafeShapedResponse(accountId, "9665****321", "Active");

        var json = AuditSnapshotRedactor.Redact(response, Options);

        json.Should().Contain(accountId.ToString());
        json.Should().Contain("9665****321");
        json.Should().Contain("Active");
    }

    [Fact]
    public void Redact_UnknownFieldNotOnAllowlist_RedactedByDefault()
    {
        var command = new FutureFieldCommand(Guid.NewGuid(), "totally-new-secret-shaped-value");

        var json = AuditSnapshotRedactor.Redact(command, Options);

        json.Should().NotContain("totally-new-secret-shaped-value");
        json.Should().Contain("\"someBrandNewField\":\"[REDACTED]\"");
    }

    [Fact]
    public void Redact_NullValue_ReturnsNull()
    {
        var result = AuditSnapshotRedactor.Redact(null, Options);

        result.Should().BeNull();
    }

    // Sheba.Shared.Kernel.Results.Result<T> shape used by T-STD-1 modules: {value:{...},
    // isSuccess, isFailure, error}. Safe fields nested inside "value" must still surface — the
    // wrapper keys themselves are structural, not data, so they shouldn't blanket-redact everything.
    private sealed record ResultLikeWrapper(SafeShapedResponse? Value, bool IsSuccess, bool IsFailure);

    [Fact]
    public void Redact_ResultWrapperAroundSafeResponse_NestedSafeFieldsSurvive()
    {
        var accountId = Guid.NewGuid();
        var wrapper = new ResultLikeWrapper(new SafeShapedResponse(accountId, "9665****321", "Active"), true, false);

        var json = AuditSnapshotRedactor.Redact(wrapper, Options);

        json.Should().Contain(accountId.ToString());
        json.Should().Contain("9665****321");
    }
}
