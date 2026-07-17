using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Identity.Application.Commands.ConfirmAdminMfa;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Domain.Interfaces;
using Sheba.Identity.Infrastructure.Security;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Tests.Application.Commands;

public sealed class ConfirmAdminMfaHandlerTests
{
    private readonly IIdentityRepository _repo = Substitute.For<IIdentityRepository>();
    private readonly IPasswordHasher _passwordHasher = new Argon2idPasswordHasher();
    private readonly ITotpService _totpService = Substitute.For<ITotpService>();
    private readonly IMfaSecretEncryptor _mfaSecretEncryptor = Substitute.For<IMfaSecretEncryptor>();
    private readonly ConfirmAdminMfaHandler _sut;

    public ConfirmAdminMfaHandlerTests()
    {
        _sut = new ConfirmAdminMfaHandler(
            _repo, _passwordHasher, _totpService, _mfaSecretEncryptor,
            NullLogger<ConfirmAdminMfaHandler>.Instance);
    }

    private static AdminUser MakeAdmin() =>
        AdminUser.Create("ADMIN001", "admin@sheba.gov", "Test Admin", AdminRole.SuperAdmin, "hash");

    [Fact]
    public async Task Handle_AdminNotFound_ReturnsNotFound()
    {
        _repo.FindAdminByIdAsync(Arg.Any<Guid>(), default).Returns((AdminUser?)null);

        var result = await _sut.Handle(new ConfirmAdminMfaCommand(Guid.NewGuid(), "123456"), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("resource");
    }

    [Fact]
    public async Task Handle_AlreadyEnabled_ReturnsConflict()
    {
        var admin = MakeAdmin();
        admin.SetMfaSecret("encrypted");
        admin.ConfirmMfaEnrollment();
        _repo.FindAdminByIdAsync(admin.Id, default).Returns(admin);

        var result = await _sut.Handle(new ConfirmAdminMfaCommand(admin.Id, "123456"), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("mfa");
    }

    [Fact]
    public async Task Handle_NoSecretEnrolled_ReturnsValidationFailure()
    {
        var admin = MakeAdmin();
        _repo.FindAdminByIdAsync(admin.Id, default).Returns(admin);

        var result = await _sut.Handle(new ConfirmAdminMfaCommand(admin.Id, "123456"), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("mfa");
    }

    [Fact]
    public async Task Handle_InvalidCode_ReturnsValidationFailure()
    {
        var admin = MakeAdmin();
        admin.SetMfaSecret("encrypted");
        _repo.FindAdminByIdAsync(admin.Id, default).Returns(admin);
        _mfaSecretEncryptor.Decrypt("encrypted").Returns("decrypted");
        _totpService.VerifyCode("decrypted", "000000").Returns(false);

        var result = await _sut.Handle(new ConfirmAdminMfaCommand(admin.Id, "000000"), default);

        result.IsFailure.Should().BeTrue();
        admin.MfaEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidCode_EnablesMfaAndReturnsTenRecoveryCodes()
    {
        var admin = MakeAdmin();
        admin.SetMfaSecret("encrypted");
        _repo.FindAdminByIdAsync(admin.Id, default).Returns(admin);
        _mfaSecretEncryptor.Decrypt("encrypted").Returns("decrypted");
        _totpService.VerifyCode("decrypted", "123456").Returns(true);

        var result = await _sut.Handle(new ConfirmAdminMfaCommand(admin.Id, "123456"), default);

        result.IsSuccess.Should().BeTrue();
        admin.MfaEnabled.Should().BeTrue();
        result.Value.RecoveryCodes.Should().HaveCount(10);
        result.Value.RecoveryCodes.Should().OnlyHaveUniqueItems();
        result.Value.RecoveryCodes.Should().OnlyContain(c => c.Length == 11 && c[5] == '-');
        await _repo.Received(1).AddAdminRecoveryCodesAsync(
            Arg.Is<IEnumerable<AdminRecoveryCode>>(codes => codes.Count() == 10), default);
    }
}
