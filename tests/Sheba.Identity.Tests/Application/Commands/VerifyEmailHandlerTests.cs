using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Identity.Application.Commands.VerifyEmail;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Infrastructure.Security;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Tests.Application.Commands;

/// <summary>
/// T-SEC-8 regression: VerifyEmailHandler used to compare the submitted token against
/// OtpRecord.CodeHash with a plain `!=` string check — which only "worked" because
/// CompleteRegistrationHandler was ALSO storing the raw token unhashed. Both are fixed together;
/// this covers the handler's half via a real Argon2idOtpHasher (hash/verify, not a mock).
/// </summary>
public sealed class VerifyEmailHandlerTests
{
    private readonly IIdentityRepository _repo = Substitute.For<IIdentityRepository>();
    private readonly IOtpHasher _otpHasher = new Argon2idOtpHasher();
    private readonly VerifyEmailHandler _sut;

    public VerifyEmailHandlerTests()
    {
        _sut = new VerifyEmailHandler(_repo, _otpHasher, NullLogger<VerifyEmailHandler>.Instance);
    }

    private static Account PendingEmailVerificationAccount()
    {
        var account = Account.CreateFromNidCheck("1000000001", "0777000001", "مواطن", "Citizen");
        account.VerifyPhone();
        account.CompleteRegistration("citizen1", "citizen1@example.com", "hashed-password");
        return account;
    }

    [Fact]
    public async Task Handle_CorrectToken_VerifiesAgainstTheHash_AndMarksEmailVerified()
    {
        var account = PendingEmailVerificationAccount();
        const string rawToken = "483920";
        var otpRecord = OtpRecord.Create(
            account.Id, OtpPurpose.EmailVerify, OtpChannel.Email, _otpHasher.Hash(rawToken), ttlMinutes: 15);

        _repo.FindAccountByIdAsync(account.Id, default).Returns(account);
        _repo.FindActiveOtpAsync(account.Id, OtpPurpose.EmailVerify, default).Returns(otpRecord);
        _repo.GetRequestsByAccountAsync(account.Id, default).Returns(new List<IdentityRequest>());

        var result = await _sut.Handle(new VerifyEmailCommand(account.Id, rawToken), default);

        result.IsSuccess.Should().BeTrue();
        account.Status.Should().Be(AccountStatus.PendingAdminApproval);
        otpRecord.UsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WrongToken_Fails_AndDoesNotMarkEmailVerified()
    {
        var account = PendingEmailVerificationAccount();
        var otpRecord = OtpRecord.Create(
            account.Id, OtpPurpose.EmailVerify, OtpChannel.Email, _otpHasher.Hash("483920"), ttlMinutes: 15);

        _repo.FindAccountByIdAsync(account.Id, default).Returns(account);
        _repo.FindActiveOtpAsync(account.Id, OtpPurpose.EmailVerify, default).Returns(otpRecord);

        var result = await _sut.Handle(new VerifyEmailCommand(account.Id, "000000"), default);

        result.IsFailure.Should().BeTrue();
        account.Status.Should().Be(AccountStatus.PendingEmailVerification);
    }

    [Fact]
    public async Task Handle_TokenThatEqualsTheStoredHashString_StillFails()
    {
        // Guards specifically against the old bug's failure mode: comparing the submitted value
        // directly against CodeHash would incorrectly succeed if a caller somehow submitted the
        // hash string itself instead of the raw code.
        var account = PendingEmailVerificationAccount();
        var hash = _otpHasher.Hash("483920");
        var otpRecord = OtpRecord.Create(
            account.Id, OtpPurpose.EmailVerify, OtpChannel.Email, hash, ttlMinutes: 15);

        _repo.FindAccountByIdAsync(account.Id, default).Returns(account);
        _repo.FindActiveOtpAsync(account.Id, OtpPurpose.EmailVerify, default).Returns(otpRecord);

        var result = await _sut.Handle(new VerifyEmailCommand(account.Id, hash), default);

        result.IsFailure.Should().BeTrue();
    }
}
