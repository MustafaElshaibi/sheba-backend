using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Identity.Application.Commands.CompleteRegistration;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Domain.Interfaces;
using Sheba.Identity.Infrastructure.Security;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Tests.Application.Commands;

/// <summary>
/// T-SEC-8: CompleteRegistrationHandler used to store the raw email-verification token directly
/// as OtpRecord.CodeHash (never hashed), and VerifyEmailHandler compared it with a plain string
/// equality check. These tests cover the fix: the handler now generates the code itself
/// (IOtpCodeGenerator), hands it to the provider to deliver, and persists only its Argon2id hash.
/// </summary>
public sealed class CompleteRegistrationHandlerTests
{
    private readonly IIdentityRepository _repo = Substitute.For<IIdentityRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IOtpProvider _otp = Substitute.For<IOtpProvider>();
    private readonly IOtpHasher _otpHasher = new Argon2idOtpHasher();
    private readonly IOtpCodeGenerator _otpCodeGenerator = new CryptoOtpCodeGenerator();
    private readonly CompleteRegistrationHandler _sut;

    public CompleteRegistrationHandlerTests()
    {
        _sut = new CompleteRegistrationHandler(
            _repo, _passwordHasher, _otp, _otpHasher, _otpCodeGenerator, NullLogger<CompleteRegistrationHandler>.Instance);

        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed-password");
        _otp.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OtpPurpose>(), Arg.Any<OtpChannel>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new OtpSendResult(true)));
    }

    private static Account PendingVerificationAccount()
    {
        var account = Account.CreateFromNidCheck("1000000001", "0777000001", "مواطن", "Citizen");
        account.VerifyPhone();
        return account;
    }

    [Fact]
    public async Task Handle_ValidRequest_GeneratesCodeInApplicationLayer_AndHandsItToProvider()
    {
        var account = PendingVerificationAccount();
        _repo.FindAccountByIdAsync(account.Id, default).Returns(account);
        _repo.FindAccountByUsernameOrNidAsync("citizen1", default).Returns((Account?)null);

        var command = new CompleteRegistrationCommand(
            account.Id, "citizen1", "citizen1@example.com", "Str0ng-Pass!", "Str0ng-Pass!");

        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        await _otp.Received(1).SendAsync(
            "citizen1@example.com", Arg.Any<string>(), OtpPurpose.EmailVerify, OtpChannel.Email, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsOnlyTheHashOfTheGeneratedCode_NeverThePlaintext()
    {
        var account = PendingVerificationAccount();
        _repo.FindAccountByIdAsync(account.Id, default).Returns(account);
        _repo.FindAccountByUsernameOrNidAsync("citizen1", default).Returns((Account?)null);

        string? sentCode = null;
        _otp.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OtpPurpose>(), Arg.Any<OtpChannel>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                sentCode = ci.ArgAt<string>(1);
                return Task.FromResult(new OtpSendResult(true));
            });

        OtpRecord? captured = null;
        _repo.AddOtpRecordAsync(Arg.Do<OtpRecord>(r => captured = r), Arg.Any<CancellationToken>());

        var command = new CompleteRegistrationCommand(
            account.Id, "citizen1", "citizen1@example.com", "Str0ng-Pass!", "Str0ng-Pass!");

        await _sut.Handle(command, default);

        sentCode.Should().NotBeNullOrEmpty();
        captured.Should().NotBeNull();
        captured!.CodeHash.Should().NotBe(sentCode); // never store the raw code
        _otpHasher.Verify(sentCode!, captured.CodeHash).Should().BeTrue(); // but the hash matches it
    }
}
