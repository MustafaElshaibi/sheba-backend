using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Identity.Application.Commands.EnrollAdminMfa;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Domain.Interfaces;

namespace Sheba.Identity.Tests.Application.Commands;

public sealed class EnrollAdminMfaHandlerTests
{
    private readonly IIdentityRepository _repo = Substitute.For<IIdentityRepository>();
    private readonly ITotpService _totpService = Substitute.For<ITotpService>();
    private readonly IMfaSecretEncryptor _mfaSecretEncryptor = Substitute.For<IMfaSecretEncryptor>();
    private readonly EnrollAdminMfaHandler _sut;

    public EnrollAdminMfaHandlerTests()
    {
        _sut = new EnrollAdminMfaHandler(
            _repo, _totpService, _mfaSecretEncryptor, NullLogger<EnrollAdminMfaHandler>.Instance);
    }

    private static AdminUser MakeAdmin() =>
        AdminUser.Create("ADMIN001", "admin@sheba.gov", "Test Admin", AdminRole.SuperAdmin, "hash");

    [Fact]
    public async Task Handle_AdminNotFound_ReturnsNotFound()
    {
        _repo.FindAdminByIdAsync(Arg.Any<Guid>(), default).Returns((AdminUser?)null);

        var result = await _sut.Handle(new EnrollAdminMfaCommand(Guid.NewGuid()), default);

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

        var result = await _sut.Handle(new EnrollAdminMfaCommand(admin.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("mfa");
    }

    [Fact]
    public async Task Handle_ValidRequest_StoresEncryptedSecretAndReturnsProvisioningUri()
    {
        var admin = MakeAdmin();
        _repo.FindAdminByIdAsync(admin.Id, default).Returns(admin);
        _totpService.GenerateSecret().Returns("RAWSECRETBASE32");
        _mfaSecretEncryptor.Encrypt("RAWSECRETBASE32").Returns("encrypted-value");
        _totpService.BuildProvisioningUri("RAWSECRETBASE32", admin.Email).Returns("otpauth://totp/test");

        var result = await _sut.Handle(new EnrollAdminMfaCommand(admin.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Secret.Should().Be("RAWSECRETBASE32");
        result.Value.ProvisioningUri.Should().Be("otpauth://totp/test");
        admin.MfaSecret.Should().Be("encrypted-value");
        admin.MfaEnabled.Should().BeFalse();
        await _repo.Received(1).SaveChangesAsync(default);
    }
}
