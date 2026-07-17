using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Identity.Application.Commands.LoginAdmin;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Domain.Interfaces;
using Sheba.Identity.Infrastructure.Security;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Tests.Application.Commands;

/// <summary>
/// Unit tests for LoginAdminHandler's password + TOTP/recovery-code gate (T-SEC-1). Uses the
/// real Argon2idPasswordHasher so hash/verify round-trips exactly as in production; ITotpService
/// and IMfaSecretEncryptor are substituted since their own correctness is covered by
/// OtpNetTotpServiceTests / AesGcmMfaSecretEncryptorTests.
/// </summary>
public sealed class LoginAdminHandlerTests
{
    private const string Password = "Correct-Horse-1";

    private readonly IIdentityRepository _repo = Substitute.For<IIdentityRepository>();
    private readonly IPasswordHasher _passwordHasher = new Argon2idPasswordHasher();
    private readonly ITotpService _totpService = Substitute.For<ITotpService>();
    private readonly IMfaSecretEncryptor _mfaSecretEncryptor = Substitute.For<IMfaSecretEncryptor>();
    private readonly LoginAdminHandler _sut;

    public LoginAdminHandlerTests()
    {
        _sut = new LoginAdminHandler(
            _repo, _passwordHasher, _totpService, _mfaSecretEncryptor, NullLogger<LoginAdminHandler>.Instance);
    }

    private AdminUser MakeAdmin(bool mfaEnabled = false)
    {
        var admin = AdminUser.Create(
            "ADMIN001", "admin@sheba.gov", "Test Admin", AdminRole.SuperAdmin, _passwordHasher.Hash(Password));

        if (mfaEnabled)
        {
            admin.SetMfaSecret("encrypted-secret");
            admin.ConfirmMfaEnrollment();
        }

        return admin;
    }

    private void SeedAdmin(AdminUser admin) =>
        _repo.FindAdminByEmployeeIdAsync(admin.EmployeeId, default).Returns(admin);

    [Fact]
    public async Task Handle_UnknownIdentifier_ReturnsGenericFailure()
    {
        _repo.FindAdminByEmployeeIdAsync("ghost", default).Returns((AdminUser?)null);
        _repo.FindAdminByEmailAsync("ghost", default).Returns((AdminUser?)null);

        var result = await _sut.Handle(new LoginAdminCommand("ghost", "whatever"), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("credentials");
    }

    [Fact]
    public async Task Handle_WrongPassword_ReturnsGenericFailure()
    {
        var admin = MakeAdmin();
        SeedAdmin(admin);

        var result = await _sut.Handle(new LoginAdminCommand(admin.EmployeeId, "wrong-password"), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("credentials");
    }

    [Fact]
    public async Task Handle_InactiveStatus_ReturnsGenericFailure()
    {
        var admin = MakeAdmin();
        admin.Suspend();
        SeedAdmin(admin);

        var result = await _sut.Handle(new LoginAdminCommand(admin.EmployeeId, Password), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("credentials");
    }

    [Fact]
    public async Task Handle_ValidCredentials_NoMfaEnrolled_ReturnsSuccess()
    {
        var admin = MakeAdmin();
        SeedAdmin(admin);

        var result = await _sut.Handle(new LoginAdminCommand(admin.EmployeeId, Password), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AdminId.Should().Be(admin.Id);
    }

    [Fact]
    public async Task Handle_MfaEnabled_NoCodeSupplied_ReturnsMfaRequired()
    {
        var admin = MakeAdmin(mfaEnabled: true);
        SeedAdmin(admin);

        var result = await _sut.Handle(new LoginAdminCommand(admin.EmployeeId, Password), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("mfa_required");
    }

    [Fact]
    public async Task Handle_MfaEnabled_ValidTotpCode_ReturnsSuccess()
    {
        var admin = MakeAdmin(mfaEnabled: true);
        SeedAdmin(admin);
        _mfaSecretEncryptor.Decrypt("encrypted-secret").Returns("decrypted-secret");
        _totpService.VerifyCode("decrypted-secret", "123456").Returns(true);

        var result = await _sut.Handle(new LoginAdminCommand(admin.EmployeeId, Password, "123456"), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MfaEnabled_InvalidTotpCode_ReturnsFailureAndIncrementsCounter()
    {
        var admin = MakeAdmin(mfaEnabled: true);
        SeedAdmin(admin);
        _mfaSecretEncryptor.Decrypt("encrypted-secret").Returns("decrypted-secret");
        _totpService.VerifyCode("decrypted-secret", "000000").Returns(false);

        var result = await _sut.Handle(new LoginAdminCommand(admin.EmployeeId, Password, "000000"), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("mfa");
        admin.MfaFailedAttempts.Should().Be(1);
    }

    [Fact]
    public async Task Handle_MfaEnabled_ValidRecoveryCode_ReturnsSuccessAndMarksCodeUsed()
    {
        var admin = MakeAdmin(mfaEnabled: true);
        SeedAdmin(admin);
        const string rawCode = "AB3D9-K7Q2M";
        var codeEntity = AdminRecoveryCode.Create(
            admin.Id, _passwordHasher.Hash(AdminRecoveryCode.Normalize(rawCode)));
        _repo.GetUnusedAdminRecoveryCodesAsync(admin.Id, default)
             .Returns(new List<AdminRecoveryCode> { codeEntity });

        var result = await _sut.Handle(new LoginAdminCommand(admin.EmployeeId, Password, rawCode), default);

        result.IsSuccess.Should().BeTrue();
        codeEntity.IsUsed.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MfaLocked_ReturnsLockedFailureWithoutConsultingTotpService()
    {
        var admin = MakeAdmin(mfaEnabled: true);
        for (var i = 0; i < 5; i++)
            admin.RecordFailedMfaAttempt();
        SeedAdmin(admin);

        var result = await _sut.Handle(new LoginAdminCommand(admin.EmployeeId, Password, "123456"), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("mfa");
        _totpService.DidNotReceive().VerifyCode(Arg.Any<string>(), Arg.Any<string>());
    }
}
