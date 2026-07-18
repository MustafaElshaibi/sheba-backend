using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Identity.Application.Commands.ConfirmPasswordReset;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Infrastructure.Security;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Tests.Application.Commands;

public sealed class ConfirmPasswordResetHandlerTests
{
    private readonly IIdentityRepository _repo = Substitute.For<IIdentityRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IOtpHasher _otpHasher = new Argon2idOtpHasher();
    private readonly ConfirmPasswordResetHandler _sut;

    public ConfirmPasswordResetHandlerTests()
    {
        _sut = new ConfirmPasswordResetHandler(
            _repo, _passwordHasher, _otpHasher, NullLogger<ConfirmPasswordResetHandler>.Instance);

        _passwordHasher.Hash(Arg.Any<string>()).Returns("new-hashed-password");
    }

    private static Account ApprovedAccount()
    {
        var account = Account.CreateFromNidCheck("1000000001", "0777000001", "مواطن", "Citizen");
        account.CompleteRegistration("citizen1", "citizen1@example.com", "old-hash");
        account.MarkEmailVerified();
        account.Approve();
        return account;
    }

    private OtpRecord ActiveResetOtp(Guid accountId, string rawCode) =>
        OtpRecord.Create(accountId, OtpPurpose.PasswordReset, OtpChannel.Sms, _otpHasher.Hash(rawCode), ttlMinutes: 5);

    [Fact]
    public async Task Handle_UnknownIdentifier_ReturnsGenericError()
    {
        _repo.FindAccountByUsernameOrNidAsync("ghost", default).Returns((Account?)null);

        var result = await _sut.Handle(
            new ConfirmPasswordResetCommand("ghost", "123456", "N3wPass!word", "N3wPass!word"), default);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoActiveOtp_ReturnsGenericError_SameAsUnknownIdentifier()
    {
        var account = ApprovedAccount();
        _repo.FindAccountByUsernameOrNidAsync("citizen1", default).Returns(account);
        _repo.FindActiveOtpAsync(account.Id, OtpPurpose.PasswordReset, default).Returns((OtpRecord?)null);

        var noOtpResult = await _sut.Handle(
            new ConfirmPasswordResetCommand("citizen1", "123456", "N3wPass!word", "N3wPass!word"), default);

        _repo.FindAccountByUsernameOrNidAsync("ghost", default).Returns((Account?)null);
        var unknownResult = await _sut.Handle(
            new ConfirmPasswordResetCommand("ghost", "123456", "N3wPass!word", "N3wPass!word"), default);

        noOtpResult.IsFailure.Should().BeTrue();
        noOtpResult.Error!.Message.Should().Be(unknownResult.Error!.Message);
    }

    [Fact]
    public async Task Handle_WrongCode_ReturnsGenericError_AndDoesNotChangeThePassword()
    {
        var account = ApprovedAccount();
        var otpRecord = ActiveResetOtp(account.Id, "654321");
        _repo.FindAccountByUsernameOrNidAsync("citizen1", default).Returns(account);
        _repo.FindActiveOtpAsync(account.Id, OtpPurpose.PasswordReset, default).Returns(otpRecord);

        var result = await _sut.Handle(
            new ConfirmPasswordResetCommand("citizen1", "000000", "N3wPass!word", "N3wPass!word"), default);

        result.IsFailure.Should().BeTrue();
        account.PasswordHash.Should().Be("old-hash");
        otpRecord.UsedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CorrectCode_ResetsPassword_AndMarksOtpUsed()
    {
        var account = ApprovedAccount();
        var otpRecord = ActiveResetOtp(account.Id, "654321");
        _repo.FindAccountByUsernameOrNidAsync("citizen1", default).Returns(account);
        _repo.FindActiveOtpAsync(account.Id, OtpPurpose.PasswordReset, default).Returns(otpRecord);

        var result = await _sut.Handle(
            new ConfirmPasswordResetCommand("citizen1", "654321", "N3wPass!word", "N3wPass!word"), default);

        result.IsSuccess.Should().BeTrue();
        account.PasswordHash.Should().Be("new-hashed-password");
        otpRecord.UsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_CorrectCode_AlsoClearsAnyExistingLockout()
    {
        var account = ApprovedAccount();
        for (var i = 0; i < 5; i++) account.RecordFailedLogin();
        account.IsLocked().Should().BeTrue();

        var otpRecord = ActiveResetOtp(account.Id, "654321");
        _repo.FindAccountByUsernameOrNidAsync("citizen1", default).Returns(account);
        _repo.FindActiveOtpAsync(account.Id, OtpPurpose.PasswordReset, default).Returns(otpRecord);

        await _sut.Handle(new ConfirmPasswordResetCommand("citizen1", "654321", "N3wPass!word", "N3wPass!word"), default);

        account.IsLocked().Should().BeFalse();
    }
}
