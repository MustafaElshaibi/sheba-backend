using FluentAssertions;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Shared.Kernel.Exceptions;

namespace Sheba.Identity.Tests.Domain;

/// <summary>
/// Unit tests for AdminUser's TOTP enrollment + lockout state machine (T-SEC-1).
/// </summary>
public sealed class AdminUserMfaTests
{
    private static AdminUser MakeAdmin() =>
        AdminUser.Create("ADMIN999", "mfa-test@sheba.gov", "MFA Test Admin", AdminRole.SuperAdmin, "hash");

    [Fact]
    public void SetMfaSecret_WhenNotEnabled_StoresSecret()
    {
        var admin = MakeAdmin();

        admin.SetMfaSecret("encrypted-secret");

        admin.MfaSecret.Should().Be("encrypted-secret");
        admin.MfaEnabled.Should().BeFalse();
    }

    [Fact]
    public void SetMfaSecret_WhenAlreadyEnabled_Throws()
    {
        var admin = MakeAdmin();
        admin.SetMfaSecret("secret-1");
        admin.ConfirmMfaEnrollment();

        var act = () => admin.SetMfaSecret("secret-2");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ConfirmMfaEnrollment_WithoutSecret_Throws()
    {
        var admin = MakeAdmin();

        var act = () => admin.ConfirmMfaEnrollment();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ConfirmMfaEnrollment_WithSecret_EnablesMfa()
    {
        var admin = MakeAdmin();
        admin.SetMfaSecret("encrypted-secret");

        admin.ConfirmMfaEnrollment();

        admin.MfaEnabled.Should().BeTrue();
    }

    [Fact]
    public void ConfirmMfaEnrollment_WhenAlreadyEnabled_Throws()
    {
        var admin = MakeAdmin();
        admin.SetMfaSecret("encrypted-secret");
        admin.ConfirmMfaEnrollment();

        var act = () => admin.ConfirmMfaEnrollment();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RecordFailedMfaAttempt_BelowThreshold_DoesNotLock()
    {
        var admin = MakeAdmin();

        for (var i = 0; i < 4; i++)
            admin.RecordFailedMfaAttempt();

        admin.MfaFailedAttempts.Should().Be(4);
        admin.IsMfaLocked().Should().BeFalse();
    }

    [Fact]
    public void RecordFailedMfaAttempt_AtFifthFailure_Locks()
    {
        var admin = MakeAdmin();

        for (var i = 0; i < 5; i++)
            admin.RecordFailedMfaAttempt();

        admin.IsMfaLocked().Should().BeTrue();
    }

    [Fact]
    public void ResetMfaFailures_ClearsCounterAndLock()
    {
        var admin = MakeAdmin();
        for (var i = 0; i < 5; i++)
            admin.RecordFailedMfaAttempt();

        admin.ResetMfaFailures();

        admin.MfaFailedAttempts.Should().Be(0);
        admin.IsMfaLocked().Should().BeFalse();
    }
}
