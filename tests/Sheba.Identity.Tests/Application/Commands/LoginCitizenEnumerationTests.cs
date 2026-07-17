using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sheba.Identity.Application.Commands.LoginCitizen;
using Sheba.Identity.Application.Interfaces;
using Sheba.Identity.Domain.Entities;
using Sheba.Identity.Domain.Enums;
using Sheba.Identity.Domain.Interfaces;
using Sheba.Identity.Infrastructure.Security;
using Sheba.Shared.Kernel.Interfaces;

namespace Sheba.Identity.Tests.Application.Commands;

/// <summary>
/// Anti-enumeration tests for login (§6.3 / BR-ON-10). The property under test: an outsider who
/// does not hold the password learns nothing from the response — not whether the identifier
/// exists, and not what state the account is in. Status feedback is only exposed *after* the
/// password is verified, because at that point the caller has proven ownership.
///
/// Uses the real Argon2idPasswordHasher so hash/verify behaves exactly as in production.
/// </summary>
public sealed class LoginCitizenEnumerationTests
{
    private const string CorrectPassword = "C0rrect-Horse-Battery!";
    private const string WrongPassword = "not-the-password";

    private readonly IIdentityRepository _repo = Substitute.For<IIdentityRepository>();
    private readonly IOtpProvider _otp = Substitute.For<IOtpProvider>();
    private readonly IPasswordHasher _passwordHasher = new Argon2idPasswordHasher();
    private readonly IOtpHasher _otpHasher = new Argon2idOtpHasher();
    private readonly LoginCitizenHandler _sut;

    public LoginCitizenEnumerationTests()
    {
        _sut = new LoginCitizenHandler(
            _repo, _otp, _passwordHasher, _otpHasher, NullLogger<LoginCitizenHandler>.Instance);

        // Default OTP send succeeds (only the Approved path reaches it).
        _otp.SendAsync(Arg.Any<string>(), Arg.Any<OtpPurpose>(), Arg.Any<OtpChannel>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult((new OtpSendResult(true), "123456")));
    }

    // ── account builders ───────────────────────────────────────────────────────

    private Account ApprovedAccount()
    {
        var acc = Account.CreateFromNidCheck("1000000001", "0777000001", "مواطن", "Citizen");
        acc.CompleteRegistration("citizen1", "c1@example.com", _passwordHasher.Hash(CorrectPassword));
        acc.MarkEmailVerified();
        acc.Approve();
        return acc;
    }

    private Account PendingApprovalAccount()
    {
        var acc = Account.CreateFromNidCheck("1000000002", "0777000002", "مواطن", "Citizen");
        acc.CompleteRegistration("citizen2", "c2@example.com", _passwordHasher.Hash(CorrectPassword));
        acc.MarkEmailVerified(); // → PendingAdminApproval
        return acc;
    }

    private Account LockedApprovedAccount()
    {
        var acc = ApprovedAccount();
        for (var i = 0; i < 5; i++) acc.RecordFailedLogin(); // 5 failures → locked
        return acc;
    }

    // ── tests ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UnknownAccount_ReturnsGenericCredentialError()
    {
        _repo.FindAccountByUsernameOrNidAsync("ghost", default).Returns((Account?)null);

        var result = await _sut.Handle(new LoginCitizenCommand("ghost", WrongPassword), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("Invalid credentials");
    }

    [Fact]
    public async Task Handle_WrongPassword_ReturnsSameGenericError_AndRecordsFailure()
    {
        var account = ApprovedAccount();
        _repo.FindAccountByUsernameOrNidAsync("citizen1", default).Returns(account);

        var result = await _sut.Handle(new LoginCitizenCommand("citizen1", WrongPassword), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("Invalid credentials");
        account.FailedLoginCount.Should().Be(1);
        await _repo.Received().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_UnknownAccountAndWrongPassword_AreIndistinguishable()
    {
        _repo.FindAccountByUsernameOrNidAsync("ghost", default).Returns((Account?)null);
        var unknown = await _sut.Handle(new LoginCitizenCommand("ghost", WrongPassword), default);

        var account = ApprovedAccount();
        _repo.FindAccountByUsernameOrNidAsync("citizen1", default).Returns(account);
        var wrongPassword = await _sut.Handle(new LoginCitizenCommand("citizen1", WrongPassword), default);

        wrongPassword.Error!.Message.Should().Be(unknown.Error!.Message);
    }

    [Fact]
    public async Task Handle_NonApprovedAccount_WithWrongPassword_DoesNotRevealStatus()
    {
        var account = PendingApprovalAccount();
        _repo.FindAccountByUsernameOrNidAsync("citizen2", default).Returns(account);

        var result = await _sut.Handle(new LoginCitizenCommand("citizen2", WrongPassword), default);

        // Status must NOT leak to someone who failed the password check.
        result.Error!.Message.Should().Contain("Invalid credentials");
        result.Error!.Message.ToLowerInvariant().Should().NotContain("approval");
        result.Error!.Message.ToLowerInvariant().Should().NotContain("pending");
    }

    [Fact]
    public async Task Handle_NonApprovedAccount_WithCorrectPassword_RevealsStatus_NoOtp()
    {
        var account = PendingApprovalAccount();
        _repo.FindAccountByUsernameOrNidAsync("citizen2", default).Returns(account);

        var result = await _sut.Handle(new LoginCitizenCommand("citizen2", CorrectPassword), default);

        // Ownership proven → the helpful status message is allowed. No OTP for non-Approved.
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("pending admin approval");
        await _otp.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<OtpPurpose>(), Arg.Any<OtpChannel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LockedAccount_WithCorrectPassword_ReturnsGenericError_WithoutRevealingLock()
    {
        var account = LockedApprovedAccount();
        _repo.FindAccountByUsernameOrNidAsync("citizen1", default).Returns(account);

        var result = await _sut.Handle(new LoginCitizenCommand("citizen1", CorrectPassword), default);

        // Locked accounts are gated before the password check and reported generically, so the
        // lock state never confirms the account exists — and no OTP is dispatched.
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("Invalid credentials");
        result.Error!.Message.ToLowerInvariant().Should().NotContain("lock");
        await _otp.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<OtpPurpose>(), Arg.Any<OtpChannel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ApprovedAccount_WithCorrectPassword_SendsOtp()
    {
        var account = ApprovedAccount();
        _repo.FindAccountByUsernameOrNidAsync("citizen1", default).Returns(account);

        var result = await _sut.Handle(new LoginCitizenCommand("citizen1", CorrectPassword), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccountId.Should().Be(account.Id);
        await _otp.Received(1).SendAsync(
            account.PhoneNumber, OtpPurpose.Login, OtpChannel.Sms, Arg.Any<CancellationToken>());
    }
}
